using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering.Universal
{
    public sealed class ShaderDebugPrintManager
    {
        private static readonly ShaderDebugPrintManager instance = new ShaderDebugPrintManager();

        private const int DebugUAVSlot = 7;
        private const int FramesInFlight = 4;
        private const int MaxBufferElements = 1024 * 16; // 16KB - must match the shader size definition

        private List<GraphicsBuffer> m_OutputBuffers = new List<GraphicsBuffer>();

        private List<Rendering.AsyncGPUReadbackRequest> m_readbackRequests =
            new List<Rendering.AsyncGPUReadbackRequest>();

        // Cache Action to avoid delegate allocation
        private Action<AsyncGPUReadbackRequest> m_bufferReadCompleteAction;

        private int m_FrameCounter = 0;
        private bool m_FrameCleared = false;

        private static readonly int m_ShaderPropertyIDInputMouse = Shader.PropertyToID("_ShaderDebugPrintInputMouse");
        private static readonly int m_ShaderPropertyIDInputFrame = Shader.PropertyToID("_ShaderDebugPrintInputFrame");

        enum DebugValueType
        {
            TypeUint = 1,
            TypeInt = 2,
            TypeFloat = 3,
            TypeUint2 = 4,
            TypeInt2 = 5,
            TypeFloat2 = 6,
            TypeUint3 = 7,
            TypeInt3 = 8,
            TypeFloat3 = 9,
            TypeUint4 = 10,
            TypeInt4 = 11,
            TypeFloat4 = 12,
            TypeBool = 13,
        };

        private const uint TypeHasTag = 128;

        static ShaderDebugPrintManager()
        {
        }

        private ShaderDebugPrintManager()
        {
            for (int i = 0; i < FramesInFlight; i++)
            {
                m_OutputBuffers.Add(new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxBufferElements, 4));
                m_readbackRequests.Add(new Rendering.AsyncGPUReadbackRequest());
            }

            m_bufferReadCompleteAction = BufferReadComplete;
        }

        public static ShaderDebugPrintManager Instance
        {
            get { return instance; }
        }

        public void SetShaderDebugPrintInputConstants(CommandBuffer cmd)
        {
            var input = ShaderDebugPrintInput.Get();

            var mouse = new Vector4(input.Pos.x, input.Pos.y, input.LeftDown ? 1 : 0, input.RightDown ? 1 : 0);
            cmd.SetGlobalVector(m_ShaderPropertyIDInputMouse, mouse);
            cmd.SetGlobalInt(m_ShaderPropertyIDInputFrame, m_FrameCounter);
        }

        public void SetShaderDebugPrintBindings(CommandBuffer cmd)
        {
            int index = m_FrameCounter % FramesInFlight;
            if (!m_readbackRequests[index].done)
            {
                // We shouldn't end up here too often
                m_readbackRequests[index].WaitForCompletion();
            }

            cmd.SetRandomWriteTarget(DebugUAVSlot, m_OutputBuffers[index]);

            ClearShaderDebugPrintBuffer();
        }

        private void ClearShaderDebugPrintBuffer()
        {
            // Only clear the buffer the first time this is called in each frame
            if (!m_FrameCleared)
            {
                int index = m_FrameCounter % FramesInFlight;
                NativeArray<uint> data = new NativeArray<uint>(1, Allocator.Temp);
                data[0] = 0;
                m_OutputBuffers[index].SetData(data, 0, 0, 1);
                m_FrameCleared = true;
            }
        }

        private void BufferReadComplete(Rendering.AsyncGPUReadbackRequest request)
        {
            Assert.IsTrue(request.done);

            if (!request.hasError)
            {
                NativeArray<uint> data = request.GetData<uint>(0);

                uint count = data[0];

                if (count >= MaxBufferElements)
                {
                    count = MaxBufferElements;
                    Debug.LogWarning("Debug Shader Print Buffer Full!");
                }

                string outputLine = "";
                if (count > 0)
                    outputLine += "Frame #" + m_FrameCounter + ": ";

                unsafe // Need to do ugly casts via pointers
                {
                    uint* ptr = (uint*)data.GetUnsafePtr();
                    for (int i = 1; i < count;)
                    {
                        DebugValueType type = (DebugValueType)(data[i] & 0x0f);
                        if ((data[i] & TypeHasTag) == TypeHasTag)
                        {
                            uint tagEncoded = data[i + 1];
                            i++;
                            for (int j = 0; j < 4; j++)
                            {
                                char c = (char)(tagEncoded & 255);
                                // skip '\0', for low-level output (avoid string termination)
                                if (c == 0)
                                    continue;
                                outputLine += c;
                                tagEncoded >>= 8;
                            }

                            outputLine += " ";
                        }

                        switch (type)
                        {
                            case DebugValueType.TypeUint:
                                outputLine += data[i + 1];
                                i += 2;
                                break;
                            case DebugValueType.TypeInt:
                                int valueInt = *(int*)&ptr[i + 1];
                                outputLine += valueInt;
                                i += 2;
                                break;
                            case DebugValueType.TypeFloat:
                                float valueFloat = *(float*)&ptr[i + 1];
                                outputLine += valueFloat;
                                i += 2;
                                break;
                            case DebugValueType.TypeUint2:
                                uint2 valueUint2 = *(uint2*)&ptr[i + 1];
                                outputLine += valueUint2;
                                i += 3;
                                break;
                            case DebugValueType.TypeInt2:
                                int2 valueInt2 = *(int2*)&ptr[i + 1];
                                outputLine += valueInt2;
                                i += 3;
                                break;
                            case DebugValueType.TypeFloat2:
                                float2 valueFloat2 = *(float2*)&ptr[i + 1];
                                outputLine += valueFloat2;
                                i += 3;
                                break;
                            case DebugValueType.TypeUint3:
                                uint3 valueUint3 = *(uint3*)&ptr[i + 1];
                                outputLine += valueUint3;
                                i += 4;
                                break;
                            case DebugValueType.TypeInt3:
                                int3 valueInt3 = *(int3*)&ptr[i + 1];
                                outputLine += valueInt3;
                                i += 4;
                                break;
                            case DebugValueType.TypeFloat3:
                                float3 valueFloat3 = *(float3*)&ptr[i + 1];
                                outputLine += valueFloat3;
                                i += 4;
                                break;
                            case DebugValueType.TypeUint4:
                                uint4 valueUint4 = *(uint4*)&ptr[i + 1];
                                outputLine += valueUint4;
                                i += 5;
                                break;
                            case DebugValueType.TypeInt4:
                                int4 valueInt4 = *(int4*)&ptr[i + 1];
                                outputLine += valueInt4;
                                i += 5;
                                break;
                            case DebugValueType.TypeFloat4:
                                float4 valueFloat4 = *(float4*)&ptr[i + 1];
                                outputLine += valueFloat4;
                                i += 5;
                                break;
                            case DebugValueType.TypeBool:
                                outputLine += ((data[i + 1] == 0) ? "False" : "True");
                                i += 2;
                                break;
                            default:
                                i = (int)count;  // Cannot handle the rest if there is an unknown type
                                break;
                        }

                        outputLine += " ";
                    }
                }

                if (count > 0)
                    Debug.Log(outputLine);
            }
            else
            {
                Debug.Log("Error at read back!");
            }
        }

        public void EndFrame()
        {
            int index = m_FrameCounter % FramesInFlight;
            m_readbackRequests[index] = Rendering.AsyncGPUReadback.Request(m_OutputBuffers[index], m_bufferReadCompleteAction);

            m_FrameCounter++;
            m_FrameCleared = false;
        }
    }

    public struct ShaderDebugPrintInput
    {
        // Mouse input
        // GameView bottom-left == (0,0) top-right == (surface.width, surface.height) where surface == game display surface/rendertarget
        // For screen pixel coordinates, game-view should be set to "Free Aspect".
        // Works only in PlayMode
        public Vector2 Pos { get; set; }
        public bool LeftDown { get; set; }
        public bool RightDown { get; set; }
        public bool MiddleDown { get; set; }

        static public ShaderDebugPrintInput Get()
        {
            var r = new ShaderDebugPrintInput();
#if ENABLE_LEGACY_INPUT_MANAGER
            r.Pos = Input.mousePosition;
            r.LeftDown = Input.GetAxis("Fire1") > 0.5f;
            r.RightDown = Input.GetAxis("Fire2") > 0.5f;
            r.MiddleDown = Input.GetAxis("Fire3") > 0.5f;
#endif
#if ENABLE_INPUT_SYSTEM
            // NOTE: needs Unity.InputSystem asmdef reference.
            var mouse = InputSystem.Mouse.current;
            r.Pos = mouse.position.ReadValue();
            r.LeftDown = mouse.leftButton.isPressed;
            r.RightDown = mouse.rightButton.isPressed;
            r.MiddleDown = mouse.middleButton.isPressed;
#endif
            return r;
        }

        public string Log()
        {
            return $"Mouse: {Pos.x}x{Pos.y}  Btns: Left:{LeftDown} Right:{RightDown} Middle:{MiddleDown} ";
        }
    }
}

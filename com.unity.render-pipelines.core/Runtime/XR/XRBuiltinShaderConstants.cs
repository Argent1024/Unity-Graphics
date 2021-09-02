using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering
{
    /// <summary>
    /// Helper static class used by render pipelines to setup stereo constants accessed by builtin shaders.
    /// </summary>
    public static class XRBuiltinShaderConstants
    {
        // References to builtin shader constants
        static public readonly int unity_StereoCameraProjection = Shader.PropertyToID("unity_StereoCameraProjection");
        static public readonly int unity_StereoCameraInvProjection = Shader.PropertyToID("unity_StereoCameraInvProjection");
        static public readonly int unity_StereoMatrixV = Shader.PropertyToID("unity_StereoMatrixV");
        static public readonly int unity_StereoMatrixInvV = Shader.PropertyToID("unity_StereoMatrixInvV");
        static public readonly int unity_StereoMatrixP = Shader.PropertyToID("unity_StereoMatrixP");
        static public readonly int unity_StereoMatrixInvP = Shader.PropertyToID("unity_StereoMatrixInvP");
        static public readonly int unity_StereoMatrixVP = Shader.PropertyToID("unity_StereoMatrixVP");
        static public readonly int unity_StereoMatrixInvVP = Shader.PropertyToID("unity_StereoMatrixInvVP");
        static public readonly int unity_StereoWorldSpaceCameraPos = Shader.PropertyToID("unity_StereoWorldSpaceCameraPos");

        // Pre-allocate arrays to avoid GC
        static Matrix4x4[] s_cameraProjMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_invCameraProjMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_viewMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_invViewMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_projMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_invProjMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_viewProjMatrix = new Matrix4x4[2];
        static Matrix4x4[] s_invViewProjMatrix = new Matrix4x4[2];
        static Vector4[] s_worldSpaceCameraPos = new Vector4[2];

        /// <summary>
        /// Populate and upload shader constants used by the C++ builtin renderer.
        /// This is required to maintain compatibility with legacy code and shaders.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="renderIntoTexture"></param>
        public static void Update(XRPass xrPass, CommandBuffer cmd, bool renderIntoTexture)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xrPass.enabled)
            {
                cmd.SetViewProjectionMatrices(xrPass.GetViewMatrix(), xrPass.GetProjMatrix());

                if (xrPass.singlePassEnabled)
                {
                    for (int viewIndex = 0; viewIndex < 2; ++viewIndex)
                    {
                        s_cameraProjMatrix[viewIndex]     = xrPass.GetProjMatrix(viewIndex);
                        s_viewMatrix[viewIndex]           = xrPass.GetViewMatrix(viewIndex);
                        s_projMatrix[viewIndex]           = GL.GetGPUProjectionMatrix(s_cameraProjMatrix[viewIndex], renderIntoTexture);
                        s_viewProjMatrix[viewIndex]       = s_projMatrix[viewIndex] * s_viewMatrix[viewIndex];

                        s_invCameraProjMatrix[viewIndex]  = Matrix4x4.Inverse(s_cameraProjMatrix[viewIndex]);
                        s_invViewMatrix[viewIndex]        = Matrix4x4.Inverse(s_viewMatrix[viewIndex]);
                        s_invProjMatrix[viewIndex]        = Matrix4x4.Inverse(s_projMatrix[viewIndex]);
                        s_invViewProjMatrix[viewIndex]    = Matrix4x4.Inverse(s_viewProjMatrix[viewIndex]);

                        s_worldSpaceCameraPos[viewIndex]  = s_invViewMatrix[viewIndex].GetColumn(3);
                    }

                    cmd.SetGlobalMatrixArray(unity_StereoCameraProjection, s_cameraProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoCameraInvProjection, s_invCameraProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixV, s_viewMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvV, s_invViewMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixP, s_projMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvP, s_invProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixVP, s_viewProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvVP, s_invViewProjMatrix);
                    cmd.SetGlobalVectorArray(unity_StereoWorldSpaceCameraPos, s_worldSpaceCameraPos);
                }
            }
#endif
        }
    }
}

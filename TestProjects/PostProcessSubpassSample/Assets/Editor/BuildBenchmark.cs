using System.Collections;
using System.Collections.Generic;
using UnityEditor;

using UnityEngine;

public class BuildBenchmark
{
    [MenuItem("Meta/Build Benchmark Project")]
    static void BuildProject()
    {
#if UNITY_EDITOR
        BuildPipeline.BuildPlayer(new string[] { "\"Assets/Scenes/Garden/GardenScene.unity" }, "PostProcessSubpass.apk", BuildTarget.Android, BuildOptions.Development);
#endif
    }
}

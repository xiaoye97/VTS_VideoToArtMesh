using System;
using BepInEx;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Video;
using System.Collections.Generic;

namespace VTS_VideoToArtMesh
{
    [BepInPlugin(GUID, PluginName, VERSION)]
    public class VideoToArtMesh : BaseUnityPlugin
    {
        public const string GUID = "me.xiaoye97.plugin.VTubeStudio.VideoToArtMesh";
        public const string PluginName = "VideoToArtMesh";
        public const string VERSION = "1.0.0";

        private void Start()
        {
            Harmony.CreateAndPatchAll(typeof(VideoToArtMesh));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(VTubeStudioModel), "Start")]
        public static void VTubeStudioModel_Start_Patch(VTubeStudioModel __instance)
        {
            FileInfo modelJSonFile = new FileInfo(__instance.ModelJSON.FilePath);
            var files = modelJSonFile.Directory.GetFiles("*.VideoToArtMesh.json");
            string modelName = modelJSonFile.Name.Replace(".vtube.json", "");
            foreach (var file in files)
            {
                if (file.Name.StartsWith(modelName))
                {
                    ReadConfigFile(__instance, file);
                }
            }
        }

        /// <summary>
        /// 读取配置文件
        /// </summary>
        public static void ReadConfigFile(VTubeStudioModel model, FileInfo file)
        {
            Debug.Log($"VideoToArtMesh 读取配置文件:{file.FullName}");
            string json = File.ReadAllText(file.FullName);
            try
            {
                var config = JsonConvert.DeserializeObject<VideoToArtMeshConfig>(json);
                if (config.ArtMeshNames == null || config.ArtMeshNames.Count == 0)
                {
                    Debug.LogWarning($"VideoToArtMesh 模型:{model.ModelJSON.Name} 的VideoToArtMesh配置文件 {file.Name} 中没有目标ArtMesh，略过");
                    return;
                }
                string videoPath = file.DirectoryName + "/" + config.VideoPath;
                if (!File.Exists(videoPath))
                {
                    Debug.LogError($"VideoToArtMesh 模型:{model.ModelJSON.Name} 的VideoToArtMesh配置文件 {file.Name} 中所配置的视频 {config.VideoPath} 不存在，略过");
                    return;
                }
                List<Renderer> renderers = new List<Renderer>();
                foreach (var d in model.Live2DModel.Drawables)
                {
                    // ID相同，找到目标
                    if (config.ArtMeshNames.Contains(d.Id))
                    {
                        var renderer = d.GetComponent<Renderer>();
                        renderers.Add(renderer);
                    }
                }
                if (renderers.Count > 0)
                {
                    float volume = Mathf.Clamp01(config.Volume);
                    foreach (var renderer in renderers)
                    {
                        var vp = renderer.gameObject.AddComponent<VideoPlayer>();
                        vp.targetMaterialRenderer = renderer;
                        var audio = renderer.gameObject.AddComponent<AudioSource>();
                        audio.volume = volume;
                        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
                        vp.SetTargetAudioSource(0, audio);
                        vp.isLooping = true;
                        vp.url = "file://" + videoPath;
                    }
                    Debug.Log($"VideoToArtMesh在模型:{model.ModelJSON.Name} 的{renderers.Count}/{config.ArtMeshNames.Count}个ArtMesh上开始播放 {config.VideoPath}，音量:{volume}");
                }
                else
                {
                    Debug.Log($"VideoToArtMesh 模型:{model.ModelJSON.Name} 上没有目标的ArtMesh，不进行视频播放");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"VideoToArtMesh解析配置文件异常 {ex}");
            }
        }
    }
}
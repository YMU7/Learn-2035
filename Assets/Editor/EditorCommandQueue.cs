using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace XRClassroom.EditorTools
{
    /// <summary>
    /// Runs editor commands dropped as text files into "Temp/EditorCommands/" (Temp is not in git),
    /// so editor steps can be scripted from outside Unity while the editor is open.
    /// Each "*.cmd" file holds one command per line; files run in name order and are deleted after.
    /// Results are written to the Console and appended to "Temp/EditorCommands/results.log".
    ///
    /// Commands:
    ///   refresh                              AssetDatabase.Refresh()
    ///   open-scene Assets/Scenes/Foo.unity   open a scene (refused if the current scene has unsaved changes)
    ///   convert-meta-rig                     run MetaRigConverter on the active scene
    ///   enable-openxr-feature MetaXRFeature  enable an OpenXR feature (by class name) for Android and Standalone
    ///   save                                 save open scenes and project assets
    ///   play 5                               enter Play mode, exit after N seconds
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCommandQueue
    {
        const string k_Folder = "Temp/EditorCommands";
        const string k_ResultsFile = k_Folder + "/results.log";

        // Polling interval in seconds (checked from EditorApplication.update).
        const double k_PollInterval = 1.0;

        // SessionState survives the domain reload that happens when entering Play mode.
        const string k_PlayUntilKey = "XRClassroom.EditorCommandQueue.PlayUntil";

        static double s_NextPoll;

        static EditorCommandQueue()
        {
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            // Exit Play mode once the requested play time is over.
            string playUntil = SessionState.GetString(k_PlayUntilKey, "");
            if (playUntil.Length > 0 && EditorApplication.isPlaying &&
                DateTime.UtcNow > DateTime.Parse(playUntil, null, System.Globalization.DateTimeStyles.RoundtripKind))
            {
                SessionState.EraseString(k_PlayUntilKey);
                EditorApplication.isPlaying = false;
                Report("play: exited Play mode");
            }

            // Do not start new commands while compiling, importing or switching Play mode.
            if (EditorApplication.timeSinceStartup < s_NextPoll ||
                EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            s_NextPoll = EditorApplication.timeSinceStartup + k_PollInterval;

            if (!Directory.Exists(k_Folder))
                return;

            string file = Directory.GetFiles(k_Folder, "*.cmd").OrderBy(path => path).FirstOrDefault();
            if (file == null)
                return;

            string[] lines = File.ReadAllLines(file);
            File.Delete(file);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                try
                {
                    Run(line);
                }
                catch (Exception exception)
                {
                    Report($"{line}: FAILED {exception.GetType().Name}: {exception.Message}", true);
                }

                // Play mode and refresh need the next editor frames; stop this file here.
                // Remaining lines are re-queued so they run after the editor settles.
                if (line.StartsWith("play") || line == "refresh")
                {
                    string[] rest = lines.Skip(i + 1).ToArray();
                    if (rest.Length > 0)
                        File.WriteAllLines(file, rest);
                    break;
                }
            }
        }

        static void Run(string line)
        {
            string[] parts = line.Split(new[] { ' ' }, 2);
            string command = parts[0];
            string argument = parts.Length > 1 ? parts[1].Trim() : "";

            switch (command)
            {
                case "refresh":
                    AssetDatabase.Refresh();
                    Report("refresh: done");
                    break;

                case "open-scene":
                    var activeScene = EditorSceneManager.GetActiveScene();
                    if (activeScene.path == argument)
                    {
                        Report($"open-scene: {argument} is already open");
                    }
                    else if (activeScene.isDirty)
                    {
                        // Never throw away unsaved work: refuse instead of opening.
                        Report($"open-scene: refused, {activeScene.path} has unsaved changes", true);
                    }
                    else
                    {
                        EditorSceneManager.OpenScene(argument, OpenSceneMode.Single);
                        Report($"open-scene: opened {argument}");
                    }
                    break;

                case "convert-meta-rig":
                    bool converted = MetaRigConverter.Convert(interactive: false);
                    Report(converted ? "convert-meta-rig: done" : "convert-meta-rig: refused (see Console)", !converted);
                    break;

                case "enable-openxr-feature":
                    EnableOpenXRFeature(argument);
                    break;

                case "save":
                    EditorSceneManager.SaveOpenScenes();
                    AssetDatabase.SaveAssets();
                    Report("save: scenes and assets saved");
                    break;

                case "play":
                    double seconds = argument.Length > 0 ? double.Parse(argument) : 5.0;
                    SessionState.SetString(k_PlayUntilKey, DateTime.UtcNow.AddSeconds(seconds).ToString("o"));
                    EditorApplication.isPlaying = true;
                    Report($"play: entering Play mode for {seconds} s");
                    break;

                default:
                    Report($"{line}: unknown command", true);
                    break;
            }
        }

        static void EnableOpenXRFeature(string featureClassName)
        {
            foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                var feature = settings == null
                    ? null
                    : settings.GetFeatures().FirstOrDefault(f => f != null && f.GetType().Name == featureClassName);

                if (feature == null)
                {
                    Report($"enable-openxr-feature: {featureClassName} not found for {group}", true);
                    continue;
                }

                if (!feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(settings);
                }
                Report($"enable-openxr-feature: {featureClassName} enabled for {group}");
            }
            AssetDatabase.SaveAssets();
        }

        static void Report(string message, bool isError = false)
        {
            string text = $"[EditorCommandQueue] {message}";
            if (isError)
                Debug.LogError(text);
            else
                Debug.Log(text);

            Directory.CreateDirectory(k_Folder);
            File.AppendAllText(k_ResultsFile, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }
    }
}

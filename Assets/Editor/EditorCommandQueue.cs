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
    ///   enable-openxr-feature HandTracking   enable an OpenXR feature (by class name) for Android and Standalone
    ///   save                                 save open scenes and project assets
    ///   play 5                               enter Play mode, exit after N seconds
    ///   menu XR Classroom/Some/Item          run an editor menu item by path
    ///   dump-scene [path]                    write the active scene's hierarchy to a text file (default Temp/EditorCommands/scene-dump.txt)
    ///   screenshot px py pz tx ty tz [fov] [file]   render from p looking at t into a PNG
    ///   raycast x z                          report every collider straight down at (x, z)
    ///   call ObjectName ComponentType Method call a public parameterless method (works in Play mode)
    /// Commands other than "play" also run while in Play mode.
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
            // Commands do run during Play mode itself (e.g. "call" and "screenshot" to test a scene).
            bool switchingPlayMode = EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;
            if (EditorApplication.timeSinceStartup < s_NextPoll ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || switchingPlayMode)
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

                case "enable-openxr-feature":
                    EnableOpenXRFeature(argument);
                    break;

                case "save":
                    EditorSceneManager.SaveOpenScenes();
                    AssetDatabase.SaveAssets();
                    Report("save: scenes and assets saved");
                    break;

                case "menu":
                    // Run any editor menu item by its path, e.g. "menu XR Classroom/Exhibits/Build Potato Battery Exhibit".
                    if (EditorApplication.ExecuteMenuItem(argument))
                        Report($"menu: ran {argument}");
                    else
                        Report($"menu: {argument} not found or failed", true);
                    break;

                case "dump-scene":
                    DumpScene(argument.Length > 0 ? argument : k_Folder + "/scene-dump.txt");
                    break;

                case "screenshot":
                    Screenshot(argument);
                    break;

                case "raycast":
                    RaycastDown(argument);
                    break;

                case "call":
                    CallMethod(argument);
                    break;

                case "play":
                    if (EditorApplication.isPlaying)
                    {
                        Report("play: already in Play mode", true);
                        break;
                    }
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

        /// <summary>
        /// Writes the active scene's hierarchy to a text file: one line per GameObject with its active state,
        /// world position, world bounds of its renderer (if any), layer and component types. Read-only.
        /// </summary>
        static void DumpScene(string path)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"# {scene.path} unsavedChanges={scene.isDirty}");

            void Write(Transform t, int depth)
            {
                var go = t.gameObject;
                string components = string.Join(",", go.GetComponents<Component>()
                    .Where(c => c != null && !(c is Transform))
                    .Select(c => c.GetType().Name));
                string bounds = "";
                if (go.TryGetComponent(out Renderer renderer))
                {
                    bounds = $" bounds(c={renderer.bounds.center:F2} size={renderer.bounds.size:F2})";
                    var material = renderer.sharedMaterial;
                    if (material != null)
                        bounds += $" mat={material.name}" + (material.HasColor("_BaseColor") ? $" color={material.GetColor("_BaseColor")}" : "");
                }
                builder.AppendLine(
                    $"{new string(' ', depth * 2)}{(go.activeSelf ? "" : "[off] ")}{go.name} " +
                    $"pos={t.position:F2} rot={t.eulerAngles:F0} scale={t.lossyScale:F2} layer={LayerMask.LayerToName(go.layer)}" +
                    $"{bounds} [{components}]");
                for (int i = 0; i < t.childCount; i++)
                    Write(t.GetChild(i), depth + 1);
            }

            foreach (var root in scene.GetRootGameObjects())
                Write(root.transform, 0);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, builder.ToString());
            Report($"dump-scene: wrote {path}");
        }

        /// <summary>
        /// "call ObjectName ComponentType Method": calls a public parameterless method on a component of the first
        /// GameObject with that name in the loaded scenes, e.g. a [ContextMenu] test hook during Play mode.
        /// </summary>
        static void CallMethod(string argument)
        {
            string[] parts = argument.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                throw new ArgumentException("usage: call ObjectName ComponentType Method (object names cannot contain spaces)");

            var target = Resources.FindObjectsOfTypeAll<Component>()
                .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.name == parts[0] && c.GetType().Name == parts[1])
                .FirstOrDefault();
            if (target == null)
            {
                Report($"call: no {parts[1]} on a GameObject named {parts[0]}", true);
                return;
            }

            var method = target.GetType().GetMethod(parts[2], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (method == null)
            {
                Report($"call: {parts[1]} has no public parameterless method {parts[2]}", true);
                return;
            }
            method.Invoke(target, null);
            Report($"call: {parts[0]}.{parts[1]}.{parts[2]}() done");
        }

        static float[] ParseFloats(string argument, int count)
        {
            var values = argument.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(count)
                .Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            if (values.Length < count)
                throw new ArgumentException($"expected {count} numbers");
            return values;
        }

        /// <summary>
        /// "screenshot px py pz tx ty tz [fov] [file]": renders the scene from a temporary camera at p looking at t
        /// into a PNG (default Temp/EditorCommands/screenshot.png). Works in Edit and Play mode.
        /// </summary>
        static void Screenshot(string argument)
        {
            string[] parts = argument.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float[] v = ParseFloats(argument, 6);
            float fov = parts.Length > 6 && float.TryParse(parts[6], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 60f;
            string file = parts.Length > 7 ? parts[7] : parts.Length > 6 && !float.TryParse(parts[6], out _) ? parts[6] : k_Folder + "/screenshot.png";

            var go = new GameObject("__EditorCommandScreenshotCamera") { hideFlags = HideFlags.HideAndDontSave };
            var renderTexture = new RenderTexture(1280, 720, 24);
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.transform.position = new Vector3(v[0], v[1], v[2]);
                camera.transform.LookAt(new Vector3(v[3], v[4], v[5]));
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.02f;
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                Report($"screenshot: wrote {file}");
            }
            finally
            {
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(go);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        /// <summary>"raycast x z": casts down from 50 m at (x, z) and reports every collider hit, top to bottom.</summary>
        static void RaycastDown(string argument)
        {
            float[] v = ParseFloats(argument, 2);
            var hits = Physics.RaycastAll(new Vector3(v[0], 50f, v[1]), Vector3.down, 100f, ~0, QueryTriggerInteraction.Collide)
                .OrderBy(h => h.distance);
            string text = string.Join("; ", hits.Select(h => $"{h.collider.name}@y={h.point.y:F3}{(h.collider.isTrigger ? "(trigger)" : "")}"));
            Report($"raycast {v[0]} {v[1]}: {(text.Length > 0 ? text : "nothing")}");
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

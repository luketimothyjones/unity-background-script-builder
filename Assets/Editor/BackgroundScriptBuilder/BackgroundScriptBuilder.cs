﻿/* Project Home: https://github.com/luketimothyjones/unity-background-script-builder/
 * License: Mozilla Public License 2.0
 * 
 * NOTE: I kindly ask that you do not publish this script (or a pluginized version of it) to the Unity Asset Store;
 * that's a right that I am explicitly reserving despite the fairly permissive license.
 * 
 * In the spirit of open source, if you make awesome changes, please make a pull request. You'll get credit for them
 * in this comment block (if you so desire).
 *
 * =====
 * Contributors:
 *    Luke Pflibsen-Jones :: Project author | GH: luketimothyjones
 *  
 */

using System;
using System.IO;
using UnityEngine;
using UnityEditor;


namespace BackgroundScriptBuilder
{
    public class BackgroundScriptBuilderWindow : EditorWindow
    {
        /* This Unity editor script listens for changes to .cs files in the provided folder and triggers
         * a script rebuild when they do. It is meant to slightly improve quality of life for developers,
         * as the build process starts as soon as the file is saved rather than when Unity gains focus.
         *
         * USAGE:
         * Place this script in Assets/Editor/
         * 
         * In the Unity menu bar, go to Window > Asset Management > Background Script Builder and place
         * the window that opens somewhere in your editor.
         * 
         * Due to the way that Unity handles GUI elements, the window must exist somewhere in the editor
         * for this to work (eg, it has to exist as a tab somewhere in your layout, but the tab doesn't
         * need to be visible).
         */

        [SerializeField]
        private BackgroundScriptBuilderApplication builderApp = null;

        [MenuItem("Window/Asset Management/Background Script Builder")]
        public static void Init()
        {
            EditorWindow.GetWindow(typeof(BackgroundScriptBuilderWindow));
        }

        void OnEnable()
        {
            if (builderApp == null) {
                builderApp = (BackgroundScriptBuilderApplication) FindObjectOfType(typeof(BackgroundScriptBuilderApplication));

                if (builderApp == null) {
                    builderApp = ScriptableObject.CreateInstance<BackgroundScriptBuilderApplication>();
                }
            }
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.Separator();

            builderApp.doBackgroundBuilds = EditorGUILayout.BeginToggleGroup("Enable Background Building", builderApp.doBackgroundBuilds);

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            if (EditorGUI.EndChangeCheck()) {
                builderApp.SaveSettings();
                builderApp.MaybeInitializeWatcher();
            }

            EditorGUI.BeginChangeCheck();

            EditorGUIUtility.labelWidth = 90;
            builderApp.scriptFolderPath = EditorGUILayout.TextField("Script Folder", builderApp.scriptFolderPath);

            if (EditorGUI.EndChangeCheck()) {
                builderApp.SaveSettings();
                builderApp.MaybeInitializeWatcher();
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Status", new GUIStyle() { fontStyle = UnityEngine.FontStyle.Bold } );
            EditorGUILayout.LabelField(builderApp.status, new GUIStyle() { wordWrap = true, padding = new RectOffset(4, 0, 0, 0) } );

            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();
        }
    }

    public class BackgroundScriptBuilderApplication : ScriptableObject
    {
        public string status;
        public bool hasInitialized;
        public bool doBackgroundBuilds = false;
        public string scriptFolderPath = "";

        private ScriptChangeWatcher scriptChangeWatcher;

        private string settingsPrefix = "wwsoft.backgroundscriptbuilder.";

        public void SaveSettings()
        {
            EditorPrefs.SetBool(settingsPrefix + "enabled", doBackgroundBuilds);
            EditorPrefs.SetString(settingsPrefix + "script_path", scriptFolderPath);
        }

        public void LoadSettings()
        {
            doBackgroundBuilds = EditorPrefs.GetBool(settingsPrefix + "enabled");
            scriptFolderPath = EditorPrefs.GetString(settingsPrefix + "script_path");
        }

        public void OnEnable() 
        {
            LoadSettings();

            if (!hasInitialized) {
                MaybeInitializeWatcher();
            }

            AssemblyReloadEvents.afterAssemblyReload += MaybeReloadInitialize;
        }

        public void OnDisable()
        {
            SaveSettings();

            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorApplication.playModeStateChanged += PlayStateInitialize;
            }

            if (scriptChangeWatcher != default(ScriptChangeWatcher) && scriptChangeWatcher != null) {
                scriptChangeWatcher.Destroy();
            }

            try {
                AssemblyReloadEvents.afterAssemblyReload -= MaybeReloadInitialize;

            } catch (Exception ex) {
                Debug.LogError(ex.Message);
            }

            status = "Disabled";
            hasInitialized = false;
        }

        void PlayStateInitialize(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) {
                MaybeReloadInitialize();
                EditorApplication.playModeStateChanged -= PlayStateInitialize;
            }
        }

        void MaybeReloadInitialize()
        {
            if (!hasInitialized) {
                MaybeInitializeWatcher();
            }
        }

        void SetState(bool _initialized, string _status)
        {
            hasInitialized = _initialized;
            status = _status;
        }

        public void MaybeInitializeWatcher()
        {
            /* Handle parsing of script folder path and manage watcher state */

            LoadSettings();

            if (scriptChangeWatcher != default(ScriptChangeWatcher)) {
                scriptChangeWatcher.Destroy();
            }

            if (!doBackgroundBuilds) {
                SetState(false, "Disabled");
                return;
            }

            if (scriptFolderPath == "") {
                SetState(false, "Disabled: No path specified");
                return;
            
            } else {
                if (scriptFolderPath == ".") {
                    scriptFolderPath = "/";
                }

                if (scriptFolderPath.StartsWith("/")) {
                    scriptFolderPath = scriptFolderPath.Substring(1);
                }

                if (!scriptFolderPath.EndsWith("/")) {
                    scriptFolderPath += "/";
                }

                bool assetsPrepended = false;
                if (!scriptFolderPath.StartsWith("Assets")) {
                    assetsPrepended = true;
                    scriptFolderPath = "Assets" + (scriptFolderPath.StartsWith("/") ? "" : "/") + scriptFolderPath;
                }

                if (!Directory.Exists(scriptFolderPath)) {
                    SetState(false, "Disabled: Path does not exist");
                    return;
                }

                if ((scriptFolderPath != "Assets/" || !assetsPrepended)) {
                    scriptChangeWatcher = new ScriptChangeWatcher();
                    if (!scriptChangeWatcher.Initialize(scriptFolderPath)) {
                        SetState(false, "Editor does not have permission to access folder \"" + scriptFolderPath + "\"");
                        return;
                    }
                }

                SetState(true, "Watching \"" + scriptFolderPath + "\" and its children");
            }
        }
    }

    public class ScriptChangeWatcher
    {
        private FileSystemWatcher watcher = null;
        private FileSystemEventHandler eventHandler = null;

        public bool Initialize(string scriptFolder)
        {
            /* Adds a watcher to the given folder that calls OnChanged whenever
             * a .cs file is modified */

            try {
                if (eventHandler == null) {
                    eventHandler = new FileSystemEventHandler(OnChanged);
                }

                if (watcher != null) {
                    watcher.Dispose();
                }

                watcher = new FileSystemWatcher(scriptFolder, "*.cs") {
                    NotifyFilter = NotifyFilters.LastWrite,
                    IncludeSubdirectories = true
                };

                watcher.Changed += eventHandler;
                watcher.EnableRaisingEvents = true;

                return true;

            } catch (System.UnauthorizedAccessException) {
                watcher.Dispose();
                return false;
            }
        }

        public void Destroy()
        {
            if (watcher != default(FileSystemWatcher) && watcher != null) {
                watcher.Dispose();
            }
        }

        public static void ScriptReloadTask()
        {
            /* This is bound to EditorApplication.update whenever a script file is changed.
             * Doing so is necessary because AssetDatebase.Refresh() does nothing if it is not
             * executed in the main thread, and this script runs in the GUI thread. */
              
            try {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                EditorApplication.update -= ScriptReloadTask;

            } catch (Exception ex) {
                Debug.LogError(ex.Message);
            }
        }

        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            EditorApplication.update += ScriptReloadTask;
        }
    }
}

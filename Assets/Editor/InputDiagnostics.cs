using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[InitializeOnLoad]
internal static class InputDiagnostics
{
    static double next;
    static InputDiagnostics() { EditorApplication.update += Capture; }
    static void Capture()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 2;
        var s = new StringBuilder();
        s.AppendLine($"play={EditorApplication.isPlaying} paused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling} focused={EditorWindow.focusedWindow?.GetType().Name} appFocus={Application.isFocused} scale={Time.timeScale} frame={Time.frameCount}");
        s.AppendLine($"update={InputSystem.settings.updateMode} background={InputSystem.settings.backgroundBehavior} editorInput={InputSystem.settings.editorInputBehaviorInPlayMode} cursor={Cursor.lockState}");
        foreach (var d in InputSystem.devices) s.AppendLine($"device={d.displayName} enabled={d.enabled} native={d.native}");
        foreach (var e in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) s.AppendLine($"eventSystem={e.name} enabled={e.isActiveAndEnabled} module={e.currentInputModule} selected={e.currentSelectedGameObject}");
        foreach (var p in Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None)) s.AppendLine($"player={p.name} enabled={p.isActiveAndEnabled} active={p.inputIsActive} map={p.currentActionMap} devices={p.devices.Count}");
        Directory.CreateDirectory(".validation");
        File.WriteAllText(".validation/live-input-state.txt", s.ToString());
    }
}

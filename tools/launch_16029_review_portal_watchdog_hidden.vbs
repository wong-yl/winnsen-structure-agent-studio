Option Explicit

Dim shell, fileSystem, scriptDirectory, watchdogPath, powerShellPath, port, command

Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

scriptDirectory = fileSystem.GetParentFolderName(WScript.ScriptFullName)
watchdogPath = fileSystem.BuildPath(scriptDirectory, "run_16029_review_portal_watchdog.ps1")
powerShellPath = shell.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\WindowsPowerShell\v1.0\powershell.exe"
port = "5180"

If WScript.Arguments.Count > 0 Then
  port = WScript.Arguments(0)
End If

command = Chr(34) & powerShellPath & Chr(34) _
  & " -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " _
  & Chr(34) & watchdogPath & Chr(34) _
  & " -Port " & port & " -PollSeconds 5"

shell.Run command, 0, False

param([string]$RequestPath)
$pipeClient = [System.IO.Pipes.NamedPipeClientStream]::new('.', 'unity-mcp-188b6503-25284', [System.IO.Pipes.PipeDirection]::InOut)
try {
 $pipeClient.Connect(10000)
 $reader = [System.IO.StreamReader]::new($pipeClient)
 $writer = [System.IO.StreamWriter]::new($pipeClient)
 $writer.AutoFlush = $true
 $hello = $reader.ReadLine() | ConvertFrom-Json
 if (!$RequestPath) { $hello.tools | Select-Object name,description | ConvertTo-Json -Depth 4; return }
 $writer.WriteLine([System.IO.File]::ReadAllText($RequestPath).Trim())
 for($n=0; $n -lt 45; $n++) { $task=$reader.ReadLineAsync(); if(!$task.Wait(10000)){Write-Output 'Timed out';break}; $line=$task.Result; if(!$line){break}; $msg=$line|ConvertFrom-Json; if($msg.type -eq 'command_in_progress'){continue}; Write-Output $line; if($msg.type -ne 'approval_pending'){break} }
} finally { $pipeClient.Dispose() }





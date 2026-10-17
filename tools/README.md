# Model and development helpers

Use `Start-Whisper.ps1` to launch the configured local developer services, `Run-Brain.cmd` or `Run-Client.cmd` for individual components, and `ModelReadiness.ps1` to inspect model availability. Read the root developer instructions before running them.

Machine-specific download/cache scripts from the private workspace are not distributed. Choose a model cache on your own computer and set the Ollama service environment before starting that service. Changing only a client's environment cannot relocate a running server's model cache.

To download the prototype's default model explicitly, use your installed Ollama CLI against the dedicated local service:

```powershell
$env:OLLAMA_HOST = '127.0.0.1:11435'
ollama pull qwen3-vl:2b-instruct-q4_K_M
```

Model installation does not prove correct visual reasoning. Use the existing readiness checks and target-computer tests before relying on guidance. This repository does not automatically download model binaries when its test suite is imported.

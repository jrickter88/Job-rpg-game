# Example data mod

`mod.example.starter-pack` is a deliberately tiny authoring fixture. It proves that a mod can
add a class and ability, reference base-game statistics, and validate without adding scripts,
assemblies, scenes, or gameplay code.

Validate the base pack and this example together from the repository root:

```powershell
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
```

To try discovery in a development build, copy the entire `mod.example.starter-pack` folder into
the Godot project's `user://mods` folder. The startup output will report one enabled data mod.

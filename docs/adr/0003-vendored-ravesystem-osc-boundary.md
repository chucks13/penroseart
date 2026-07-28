# The vendored OSC library stays separate

`Assets/OSC/*.cs` is imported from RaveSystem, a separately-owned project, and adapted for Unity,
so those files keep a copyright and `Origin:` header recording where they came from and who owns
them. That header pattern belongs to the imported code alone and is never applied to anything
else. Penrose/Rave application policy lives in `Assets/OSC/Rave/` or in core consumers, never in
the vendored files.

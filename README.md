# Stable Enum for Unity

**Stable Enum** is a Unity package that prevents Inspector-serialized enum values from breaking when you reorder, insert, or remove enum members.

It automatically detects changes to enums marked with `[StableEnum]` and migrates serialization data in Scenes, Prefabs, and ScriptableObjects to match the new integer values, keeping your data safe.

## Features
- **Automatic Migration**: Detects enum changes on compilation and updates assets automatically.
- **Zero Boilerplate**: Just add an attribute.
- **Project Settings Storage**: Saves enum history in `ProjectSettings/StableEnumRegistry.json` (version control friendly).
- **Manual Control**: Includes a dashboard at `Tools > StableEnum` to view registered enums and logs.

## Installation

### via Unity Package Manager
1. Open **Window > Package Manager**.
2. Click the **+** button in the top-left corner.
3. Select **Add package from git URL...**.
4. Enter the URL of this repository:
   ```
   https://github.com/your-username/your-repo-name.git
   ```

## Usage

1. Add the `[StableEnum]` attribute to your enum definition.

```csharp
using StableEnum;

[StableEnum]
public enum WeaponType
{
    Sword,
    Bow,
    Magic
}
```

2. That's it! You can now safely insert new members in the middle or reorder them. The plugin will automatically remap the serialized integer values in your project.

### Example Scenario
**Before:**
```csharp
public enum MyEnum { A, B, C } 
// A=0, B=1, C=2
// Inspector value "B" is saved as 1
```

**Change:** You insert `NEW` at the beginning.
```csharp
[StableEnum]
public enum MyEnum { NEW, A, B, C }
// NEW=0, A=1, B=2, C=3
```

**Result:**
Without this package, the Inspector value `1` would now point to `A` (wrong).  
With **Stable Enum**, the serialized value `1` is automatically updated to `2` (B), preserving your data.

## License
MIT License

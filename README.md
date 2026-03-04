# Morpeh Source Generator

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2021.3+-blue.svg)](https://unity3d.com/get-unity/download)

A simple Source Generator for [Morpeh ECS](https://github.com/scellecs/morpeh) that automatically generates stash initialization code. No extra features, just eliminates boilerplate.

## Installation

Library distributed as git
package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))

&nbsp;&nbsp;&nbsp;⭐ Main: https://github.com/ese9/Morpeh.SourceGenerator.git  
&nbsp;&nbsp;&nbsp;🏷️ Tag:  https://github.com/ese9/Morpeh.SourceGenerator.git#1.0.0

## Problem

In Morpeh ECS, you need to manually initialize stashes for each component type in every system. This leads to repetitive
boilerplate code. This package helps eliminate this redundant code by automatically generating stash initialization.

### Before

```csharp
public class ProjectileCollisionSystem : ISystem
{
    private Filter filter;
    private Stash<Projectile> projectileStash;
    private Stash<EntityCollisions> entityCollisionsStash;
    private Stash<DestroyRequest> destroyStash;
    
    public World World { get; set; }
    
    public void OnAwake()
    {
        filter = World.Filter
                     .With<Projectile>()
                     .With<EntityCollisions>()
                     .Build();
                     
        projectileStash = World.GetStash<Projectile>();
        entityCollisionsStash = World.GetStash<EntityCollisions>();
        destroyStash = World.GetStash<DestroyRequest>();
    }
    
    public void OnUpdate(float deltaTime)
    {
        foreach (var entity in filter)
        {
            ref var collisions = ref entityCollisionsStash.Get(entity);

            if (collisions.HasEntityContacts || collisions.HasOtherContacts)
            {
                destroyStash.Add(entity);
            }
        }
    }
    
    public void Dispose() { }
}
```

### After

> [!NOTE]
> Stash variable names are automatically generated from component type name + "Stash" suffix (e.g., `Projectile` →
`projectileStash`, `EntityCollisions` → `entityCollisionsStash`).
> You can also specify a custom variable name explicitly as a second parameter (e.g.,
`[GetStash(typeof(DestroyRequest), "destroyStash")]`)

```csharp
[GetStash(typeof(Projectile))]
[GetStash(typeof(EntityCollisions))]
[GetStash(typeof(DestroyRequest), "destroyStash")]
public partial class ProjectileCollisionSystem : SourceGenSystem
{
    private Filter filter;

    protected override void InitializeFilters()
    {
        filter = World.Filter
                     .With<Projectile>()
                     .With<EntityCollisions>()
                     .Build();
    }

    public override void OnUpdate(float deltaTime)
    {
        foreach (var entity in filter)
        {
            ref var collisions = ref entityCollisionsStash.Get(entity);

            if (collisions.HasEntityContacts || collisions.HasOtherContacts)
            {
                destroyStash.Add(entity);
            }
        }
    }
}
```

## Available Base Systems

- `SourceGenSystem` - Regular system (ISystem)
- `SourceGenFixedSystem` - Fixed Update system (IFixedSystem)
- `SourceGenLateSystem` - Late Update system (ILateSystem)
- `SourceGenCleanupSystem` - Cleanup system (ICleanupSystem)
- `SourceGenInitializer` - Initializer (IInitializer)

## License

MIT License - see [LICENSE](LICENSE.md) file for details.


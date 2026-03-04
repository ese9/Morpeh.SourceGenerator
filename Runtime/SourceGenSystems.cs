using System.Runtime.CompilerServices;

namespace Scellecs.Morpeh.SourceGenerator
{
    public abstract class SourceGenFixedSystem : SourceGenSystem, IFixedSystem
    {
    }

    public abstract class SourceGenLateSystem : SourceGenSystem, ILateSystem
    {
    }

    public abstract class SourceGenCleanupSystem : SourceGenSystem, ICleanupSystem
    {
    }

    public abstract class SourceGenSystem : SourceGenInitializer, ISystem
    {
        public override void OnAwake()
        {
            InitializeFilters();

            base.OnAwake();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void InitializeFilters();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void OnUpdate(float deltaTime);
    }

    public abstract class SourceGenInitializer : IInitializer
    {
        public World World { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void OnAwake()
        {
            InitializeStashes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void InitializeStashes()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Dispose()
        {
        }
    }
}
using System;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Batches material texture and sampler bindings, skipping unchanged bindings between a section's light passes.
/// Reset before drawing a new section or after any other code changes pixel shader resources or samplers.
/// Only material textures are tracked; render-target bindings can be implicitly unbound by D3D11
/// and must not be cached across a change to the output targets.
/// </summary>
public sealed class PixelShaderResourceBindings
{
    private const int SlotCount = 128;
    private readonly ShaderResourceView[] Resources = new ShaderResourceView[SlotCount];
    private readonly ShaderResourceView[] BindingRange = new ShaderResourceView[SlotCount];
    private readonly bool[] Known = new bool[SlotCount];
    private readonly bool[] Dirty = new bool[SlotCount];
    private int FirstDirty = SlotCount;
    private int LastDirty = -1;

    private const int SamplerSlotCount = 16;
    private readonly SamplerState[] Samplers = new SamplerState[SamplerSlotCount];
    private readonly SamplerState[] SamplerBindingRange = new SamplerState[SamplerSlotCount];
    private readonly bool[] KnownSamplers = new bool[SamplerSlotCount];
    private readonly bool[] DirtySamplers = new bool[SamplerSlotCount];
    private int FirstDirtySampler = SamplerSlotCount;
    private int LastDirtySampler = -1;

    public void Reset()
    {
        Array.Clear(Samplers);
        Array.Clear(SamplerBindingRange);
        Array.Clear(KnownSamplers);
        Array.Clear(DirtySamplers);
        FirstDirtySampler = SamplerSlotCount;
        LastDirtySampler = -1;
        Array.Clear(Resources);
        Array.Clear(BindingRange);
        Array.Clear(Known);
        Array.Clear(Dirty);
        FirstDirty = SlotCount;
        LastDirty = -1;
    }

    public void Set(int slot, ShaderResourceView resource)
    {
        if (Known[slot] && ReferenceEquals(Resources[slot], resource)) return;
        Resources[slot] = resource;
        Known[slot] = true;
        Dirty[slot] = true;
        FirstDirty = Math.Min(FirstDirty, slot);
        LastDirty = Math.Max(LastDirty, slot);
    }

    public void SetSampler(int slot, SamplerState sampler)
    {
        if (KnownSamplers[slot] && ReferenceEquals(Samplers[slot], sampler)) return;
        Samplers[slot] = sampler;
        KnownSamplers[slot] = true;
        DirtySamplers[slot] = true;
        FirstDirtySampler = Math.Min(FirstDirtySampler, slot);
        LastDirtySampler = Math.Max(LastDirtySampler, slot);
    }

    public void Apply(PixelShaderStage stage)
    {
        // Submit contiguous changed slots together, leaving gaps untouched.
        int slot = FirstDirty;
        while (slot <= LastDirty)
        {
            if (!Dirty[slot])
            {
                slot++;
                continue;
            }
            int start = slot++;
            while (slot <= LastDirty && Dirty[slot]) slot++;
            int count = slot - start;
            Array.Copy(Resources, start, BindingRange, 0, count);
            stage.SetShaderResources(start, count, BindingRange);
            Array.Clear(Dirty, start, count);
        }
        FirstDirty = SlotCount;
        LastDirty = -1;

        slot = FirstDirtySampler;
        while (slot <= LastDirtySampler)
        {
            if (!DirtySamplers[slot])
            {
                slot++;
                continue;
            }
            int start = slot++;
            while (slot <= LastDirtySampler && DirtySamplers[slot]) slot++;
            int count = slot - start;
            Array.Copy(Samplers, start, SamplerBindingRange, 0, count);
            stage.SetSamplers(start, count, SamplerBindingRange);
            Array.Clear(DirtySamplers, start, count);
        }
        FirstDirtySampler = SamplerSlotCount;
        LastDirtySampler = -1;
    }
}

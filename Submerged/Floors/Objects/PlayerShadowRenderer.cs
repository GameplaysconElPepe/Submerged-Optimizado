using Reactor.Utilities.Attributes;
using UnityEngine;

namespace Submerged.Floors.Objects;

[RegisterInIl2Cpp]
public class PlayerShadowRenderer(nint ptr) : RelativeShadowRenderer(ptr)
{
    public PlayerControl player;

    // EnableShadow cannot change mid-frame (player state only updates between frames), so the
    // result is memoized per frame. Every body part of a player queries this every frame.
    private int _enableShadowFrame = -1;
    private bool _enableShadowValue;

    protected override void Start()
    {
        base.Start();
        player = GetComponentInParent<PlayerControl>();
    }

    public override bool EnableShadow
    {
        get
        {
            int frame = Time.frameCount;
            if (_enableShadowFrame == frame) return _enableShadowValue;

            _enableShadowValue = player && player.Data && (player.isDummy || (!player.Data.IsDead && !player.Data.Disconnected));
            _enableShadowFrame = frame;

            return _enableShadowValue;
        }
    }
}

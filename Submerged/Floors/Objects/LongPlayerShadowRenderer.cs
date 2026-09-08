using Reactor.Utilities.Attributes;
using UnityEngine;

namespace Submerged.Floors.Objects;

[RegisterInIl2Cpp]
public class LongPlayerShadowRenderer(nint ptr) : PlayerShadowRenderer(ptr)
{
    public LongBoiPlayerBody body;

    protected override void Start()
    {
        base.Start();
        body = target.GetComponentsInParent<LongBoiPlayerBody>(true)[0];
        body.gameObject.layer = LayerMask.NameToLayer("Players");
    }

    private SpriteRenderer _lastRenderer;

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (!targetRenderer || _lastRenderer == targetRenderer) return;

        _lastRenderer = targetRenderer;

        switch (targetRenderer.name)
        {
            case "LongNeck":
            {
                Vector2 size = new(targetRenderer.size.x, 1.1f);
                shadowRenderer.size = size;
                TrackShadowSize(size);
                break;
            }

            case "ForegroundNeck":
            {
                Vector2 size = new(targetRenderer.size.x, 1.7f);
                shadowRenderer.size = size;
                TrackShadowSize(size);
                break;
            }

            case "LongHead":
            {
                Vector3 position = new(shadowRenderer.transform.localPosition.x, body.neckSprite.transform.localPosition.y + 2.79f, shadowRenderer.transform.localPosition.z);
                shadowRenderer.transform.localPosition = position;
                TrackShadowLocalPosition(position);
                break;
            }
        }
    }
}

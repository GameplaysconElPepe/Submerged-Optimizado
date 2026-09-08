using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using Reactor.Utilities.Attributes;
using Submerged.Extensions;
using UnityEngine;

namespace Submerged.Floors.Objects;

[RegisterInIl2Cpp]
public class RelativeShadowRenderer(nint ptr) : MonoBehaviour(ptr)
{
    public Transform target;
    public SpriteRenderer targetRenderer;

    public bool isRoot;
    public Sprite[] replacementSprites;
    public SpriteRenderer shadowRenderer;

    // Last values written to the shadow renderer. They let LateUpdate skip redundant interop
    // property writes; the shadow renderer always ends up with the exact same values as before.
    private bool _syncedOnce;
    private Vector3 _lastLocalPosition;
    private Vector3 _lastLocalScale;
    private Quaternion _lastLocalRotation;
    private bool _lastEnabled;
    private nint _lastSpritePointer = (nint) (-1);
    private float _lastAlpha;
    private bool _lastFlipX;
    private bool _lastFlipY;
    private Vector2 _lastSize;
    private SpriteDrawMode _lastDrawMode;
    private SpriteTileMode _lastTileMode;
    private float _lastAdaptiveThreshold;
    private GameObject _targetRendererGameObject;

    private readonly Dictionary<Sprite, Sprite> _cachedSprites = [];

    public virtual bool EnableShadow => true;

    protected virtual void Awake()
    {
        gameObject.layer = 4;
    }

    protected virtual void Start()
    {
        shadowRenderer = gameObject.AddComponent<SpriteRenderer>();
        this.StartCoroutine(UpdateTargetRenderer());
    }

    protected virtual void LateUpdate()
    {
        if (!ShipStatus.Instance) return;

        Transform objTransform = transform;

        if (isRoot)
        {
            WriteLocalPosition(objTransform, new Vector3(-0.04f, 0, 0)); // slight offset in the shadow idk why
            WriteLocalScale(objTransform, Vector3.one);
            WriteLocalRotation(objTransform, Quaternion.identity);
        }
        else if (target)
        {
            // target.transform always resolves to target itself, so read from it directly
            WriteLocalPosition(objTransform, target.localPosition);
            WriteLocalScale(objTransform, target.localScale);
            WriteLocalRotation(objTransform, target.localRotation);
        }

        if (!targetRenderer) return;

        GameObject targetRendererGameObject = _targetRendererGameObject;
        if (targetRendererGameObject is null) targetRendererGameObject = _targetRendererGameObject = targetRenderer.gameObject;

        // Equivalent to targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy
        bool enabled = targetRenderer.enabled && targetRendererGameObject.activeInHierarchy && EnableShadow;

        if (!_syncedOnce || enabled != _lastEnabled)
        {
            _lastEnabled = enabled;
            shadowRenderer.enabled = enabled;
        }

        // Sprite identity is compared by native pointer to avoid an interop equality call
        Sprite sprite = targetRenderer.sprite;
        nint spritePointer = sprite is null ? nint.Zero : sprite.Pointer;

        if (!_syncedOnce || spritePointer != _lastSpritePointer)
        {
            _lastSpritePointer = spritePointer;
            shadowRenderer.sprite = GetReplacementSprite(sprite);
        }

        float alpha = targetRenderer.color.a;

        if (!_syncedOnce || alpha != _lastAlpha)
        {
            _lastAlpha = alpha;
            Color color = shadowRenderer.color;
            color.a = alpha;
            shadowRenderer.color = color;
        }

        bool flipX = targetRenderer.flipX;

        if (!_syncedOnce || flipX != _lastFlipX)
        {
            _lastFlipX = flipX;
            shadowRenderer.flipX = flipX;
        }

        bool flipY = targetRenderer.flipY;

        if (!_syncedOnce || flipY != _lastFlipY)
        {
            _lastFlipY = flipY;
            shadowRenderer.flipY = flipY;
        }

        SpriteDrawMode drawMode = targetRenderer.drawMode;

        if (!_syncedOnce || drawMode != _lastDrawMode)
        {
            _lastDrawMode = drawMode;
            shadowRenderer.drawMode = drawMode;
        }

        // In Simple draw mode size/tileMode/adaptiveModeThreshold have no visual effect on either
        // renderer, so they only need to be mirrored for non-simple draw modes.
        if (drawMode != SpriteDrawMode.Simple)
        {
            Vector2 size = targetRenderer.size;

            if (!_syncedOnce || !size.Equals(_lastSize))
            {
                _lastSize = size;
                shadowRenderer.size = size;
            }

            SpriteTileMode tileMode = targetRenderer.tileMode;

            if (!_syncedOnce || tileMode != _lastTileMode)
            {
                _lastTileMode = tileMode;
                shadowRenderer.tileMode = tileMode;
            }

            float adaptiveModeThreshold = targetRenderer.adaptiveModeThreshold;

            if (!_syncedOnce || adaptiveModeThreshold != _lastAdaptiveThreshold)
            {
                _lastAdaptiveThreshold = adaptiveModeThreshold;
                shadowRenderer.adaptiveModeThreshold = adaptiveModeThreshold;
            }
        }

        _syncedOnce = true;
    }

    private void WriteLocalPosition(Transform shadowTransform, Vector3 value)
    {
        if (_syncedOnce && value.Equals(_lastLocalPosition)) return;

        _lastLocalPosition = value;
        shadowTransform.localPosition = value;
    }

    private void WriteLocalScale(Transform shadowTransform, Vector3 value)
    {
        if (_syncedOnce && value.Equals(_lastLocalScale)) return;

        _lastLocalScale = value;
        shadowTransform.localScale = value;
    }

    private void WriteLocalRotation(Transform shadowTransform, Quaternion value)
    {
        if (_syncedOnce && value.Equals(_lastLocalRotation)) return;

        _lastLocalRotation = value;
        shadowTransform.localRotation = value;
    }

    // Subclasses that override shadow values directly (e.g. LongPlayerShadowRenderer) must keep
    // the write-elision caches in sync, so that the next comparison behaves exactly like the
    // original unconditional writes did.
    protected void TrackShadowLocalPosition(Vector3 actualValue) => _lastLocalPosition = actualValue;

    protected void TrackShadowSize(Vector2 actualValue) => _lastSize = actualValue;

    private Sprite GetReplacementSprite(Sprite spriteToGet)
    {
        if (!spriteToGet) return null;

        if (_cachedSprites.TryGetValue(spriteToGet, out Sprite cachedShadowSprite) && cachedShadowSprite)
        {
            return cachedShadowSprite;
        }

        string spriteName = spriteToGet.name;
        Sprite newShadowSprite = replacementSprites.FirstOrDefault(s => s.name == spriteName);

        return _cachedSprites[spriteToGet] = newShadowSprite ? newShadowSprite : spriteToGet;
    }

    [HideFromIl2Cpp]
    private IEnumerator UpdateTargetRenderer()
    {
        // A single loop with a reused WaitForSeconds avoids allocating two objects every second
        // per shadow renderer (there can be hundreds of them).
        WaitForSeconds wait = new WaitForSeconds(1);

        while (true)
        {
            targetRenderer = target.GetComponent<SpriteRenderer>();
            _targetRendererGameObject = targetRenderer ? targetRenderer.gameObject : null;
            yield return wait;
        }
    }
}

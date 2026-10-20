using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only player summary on the left page; inventory keeps ownership of the right page.
public sealed class NotebookPlayerInfoView : MonoBehaviour
{
    private PlayerHealth _health;
    private PlayerStressController _stress;
    private PlayerInventory _inventory;
    private NetPlayerStatus _identity;
    private PlayerTalismanEffectController _effects;
    private PlayerTalismanCollection _talismans;
    private TMP_Text _effectStatus;
    private readonly System.Text.StringBuilder _effectText = new();
    private TMP_Text _name, _hp, _stressValue, _condition, _items;
    private RectTransform _hpFill, _stressFill;
    private float _nextRefresh;

    public void Bind(GameObject player, TMP_FontAsset font)
    {
        _health = player.GetComponent<PlayerHealth>(); _stress = player.GetComponent<PlayerStressController>();
        _inventory = player.GetComponent<PlayerInventory>(); _identity = player.GetComponent<NetPlayerStatus>();
        _effects = player.GetComponent<PlayerTalismanEffectController>();
        _talismans = player.GetComponent<PlayerTalismanCollection>();
        PlayerItemUIStyle.NotebookLabel(transform, font, "PlayerHeading", "플레이어 정보", 20, 16, 440, 42, 30);
        _name = PlayerItemUIStyle.NotebookLabel(transform, font, "PlayerName", "플레이어", 20, 90, 440, 52, 32);
        _condition = PlayerItemUIStyle.NotebookLabel(transform, font, "Condition", "", 20, 148, 440, 32, 20);
        PlayerItemUIStyle.NotebookLabel(transform, font, "HealthHeading", "체력", 20, 210, 210, 36, 24);
        _hp = PlayerItemUIStyle.NotebookLabel(transform, font, "HealthValue", "", 236, 210, 224, 36, 23);
        _hp.alignment = TextAlignmentOptions.Right;
        _hpFill = Bar("HealthBar", 260, new Color(.34f, .43f, .28f, 1));
        PlayerItemUIStyle.NotebookLabel(transform, font, "StressHeading", "스트레스", 20, 316, 210, 36, 24);
        _stressValue = PlayerItemUIStyle.NotebookLabel(transform, font, "StressValue", "", 236, 316, 224, 36, 23);
        _stressValue.alignment = TextAlignmentOptions.Right;
        _stressFill = Bar("StressBar", 366, new Color(.60f, .32f, .17f, 1));
        _items = PlayerItemUIStyle.NotebookLabel(transform, font, "Possessions", "", 20, 430, 440, 74, 22);
        _effectStatus = PlayerItemUIStyle.NotebookLabel(transform, font, "ActiveTalismanEffects", "", 20, 504, 440, 90, 18);
        var hint = PlayerItemUIStyle.NotebookLabel(transform, font, "InventoryHint",
            "오른쪽 소지품에서 퀵슬롯 등록 · 공용 재료는 제작 탭", 20, 602, 440, 30, 16);
        hint.color = PlayerItemUIStyle.NotebookMuted;
        Refresh();
    }

    private RectTransform Bar(string name, float y, Color color)
    {
        var rect = PlayerItemUIStyle.NotebookRect(transform, name, 20, y, 440, 14);
        rect.gameObject.AddComponent<Image>().color = PlayerItemUIStyle.NotebookCard;
        var fill = PlayerItemUIStyle.NotebookRect(rect, "Fill", 0, 0, 440, 14);
        fill.gameObject.AddComponent<Image>().color = color;
        return fill;
    }
    private void LateUpdate()
    {
        PlayerItemUIStyle.FitNotebookSpread((RectTransform)transform);
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .25f; Refresh();
    }
    public void Refresh()
    {
        if (_name == null) return;
        _name.text = _identity != null && !string.IsNullOrWhiteSpace(_identity.PlayerName) ? _identity.PlayerName : "플레이어";
        _hp.text = _health != null ? $"{_health.CurrentHealth:0} / {_health.MaxHealth:0}" : "—";
        _stressValue.text = _stress != null ? $"{_stress.CurrentStress:0} / {_stress.MaxStress:0}" : "—";
        _hpFill.sizeDelta = new Vector2(440 * (_health != null ? Mathf.Clamp01(_health.NormalizedHealth) : 0), 14);
        _stressFill.sizeDelta = new Vector2(440 * (_stress != null ? Mathf.Clamp01(_stress.NormalizedStress) : 0), 14);
        _condition.text = _health != null && _health.IsDead ? "관전 중" : _stress == null ? "" :
            _stress.CurrentLevel == PlayerStressController.StressLevel.High ? "긴장이 매우 높습니다" :
            _stress.CurrentLevel == PlayerStressController.StressLevel.Mid ? "긴장한 상태입니다" : "안정된 상태입니다";
        int count = 0;
        if (_inventory != null) foreach (var item in _inventory.Items) if (item != null && item.Category != ItemCategory.Material) count++;
        var talismans = _inventory != null ? _inventory.GetComponent<PlayerTalismanCollection>() : null;
        count += talismans != null ? talismans.DrawnEffectCount : 0;
        _items.text = $"개인 소지품  {count}개\n퀵슬롯  1 ~ 6";
        RefreshEffects();
    }

    private void RefreshEffects()
    {
        _effectText.Clear();
        if (_effects != null && PlayerActionGuard.CanAct(_effects.gameObject))
        {
            foreach (var state in _effects.ExportState())
            {
                TalismanEffectDefinition definition = null;
                if (_talismans != null)
                    foreach (var item in _talismans.ItemTypes)
                        if ((definition = item.FindEffect(state.EffectId)) != null) break;
                if (definition == null) continue;
                if (_effectText.Length > 0) _effectText.AppendLine();
                _effectText.Append(definition.ModifierDescription).Append(" · ")
                    .Append(Mathf.CeilToInt(state.RemainingDuration)).Append("초 남음");
            }
        }
        _effectStatus.text = _effectText.Length > 0 ? _effectText.ToString() : "적용 중인 부적 효과 없음";
    }
}

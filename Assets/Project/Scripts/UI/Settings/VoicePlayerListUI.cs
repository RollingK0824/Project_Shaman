using UnityEngine;

// 플레이어별 음소거 목록. 로컬 플레이어는 뺌. 게임 씬에서만 채워짐 (로비/메인 메뉴는 비어 있음)
public class VoicePlayerListUI : MonoBehaviour
{
    [SerializeField]
    private VoicePlayerRowUI _rowPrefab;

    [SerializeField]
    private Transform _content;

    // 목록이 비었을 때 표시 ("게임 중에만 표시")
    [SerializeField]
    private GameObject _emptyText;

    private void OnEnable()
    {
        NetPlayerVoice.ListChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        NetPlayerVoice.ListChanged -= Rebuild;
    }

    private void Rebuild()
    {
        if (_rowPrefab == null || _content == null)
        {
            return;
        }

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            Destroy(_content.GetChild(i).gameObject);
        }

        int count = 0;
        foreach (NetPlayerVoice player in NetPlayerVoice.All)
        {
            if (player == null || player.IsLocal)
            {
                continue;
            }

            VoicePlayerRowUI row = Instantiate(_rowPrefab, _content);
            row.Bind(player);
            count++;
        }

        if (_emptyText != null)
        {
            _emptyText.SetActive(count == 0);
        }
    }
}

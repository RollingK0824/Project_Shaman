using UnityEngine;

public class LobbyCharacterPreview : MonoBehaviour
{
    [SerializeField] private GameObject[] _models;
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _turntable;
    [SerializeField] private float _rotateSpeed = 20f;

    private int _shown = int.MinValue;

    private void Awake()
    {
        Show(-1);
    }

    public void Show(int characterId)
    {
        if (characterId == _shown) return;
        _shown = characterId;

        bool valid = characterId >= 0 && characterId < _models.Length;
        for (int i = 0; i<_models.Length; i++)
        {
            if (_models[i] != null) _models[i].SetActive(i == characterId);
        }

        if (_camera != null) _camera.enabled = valid;
    }

    private void Update()
    {
        if (_turntable != null && _camera != null && _camera.enabled)
        {
            _turntable.Rotate(0f, _rotateSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }
}

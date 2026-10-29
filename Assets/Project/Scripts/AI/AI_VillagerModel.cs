using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI
{
    // One villager prefab for both genders: the model under it is swapped once the gender is known
    // (server: before spawn, client: when the public info arrives), so every client builds the same body
    [RequireComponent(typeof(AI_Core))]
    public class AI_VillagerModel : MonoBehaviour
    {
        [SerializeField] private GameObject _maleModel;
        [SerializeField] private GameObject _femaleModel;
        [Tooltip("Model instance placed in the prefab")]
        [SerializeField] private Transform _currentModel;
        [Tooltip("Gender of the model placed in the prefab, kept when the villager has the same gender")]
        [SerializeField] private VillagerGender _placedGender = VillagerGender.Male;

        private AI_Core _core;
        private VillagerGender _currentGender;

        // The new model's Animator, for components that hold one (visual controller, NetworkAnimator)
        public event Action<Animator> OnModelChanged;

        // The body now in use (tilted by AI_Bump)
        public Transform CurrentModel => _currentModel;

        private void Awake()
        {
            _core = GetComponent<AI_Core>();
            _currentGender = _placedGender;
            _core.OnPublicInfoApplied += HandlePublicInfo;
        }

        private void OnDestroy()
        {
            if (_core != null)
            {
                _core.OnPublicInfoApplied -= HandlePublicInfo;
            }
        }

        private void HandlePublicInfo(VillagerPublicInfo info)
        {
            if (info != null)
            {
                Apply(info.Gender);
            }
        }

        private void Apply(VillagerGender gender)
        {
            GameObject prefab = gender == VillagerGender.Female ? _femaleModel : _maleModel;
            if (gender == _currentGender || prefab == null || _currentModel == null)
            {
                return;
            }

            Transform old = _currentModel;
            GameObject model = Instantiate(prefab, old.parent);
            model.name = prefab.name;
            model.transform.SetLocalPositionAndRotation(old.localPosition, old.localRotation);
            model.transform.localScale = old.localScale;
            model.transform.SetSiblingIndex(old.GetSiblingIndex());

            // Out of the hierarchy at once, so renderer and Animator lookups this frame only find the new body
            old.gameObject.SetActive(false);
            old.SetParent(null);
            Destroy(old.gameObject);

            _currentModel = model.transform;
            _currentGender = gender;
            AILog.Log(AILog.CORE, _core.LogId, $"Model {prefab.name} ({gender})");
            OnModelChanged?.Invoke(model.GetComponentInChildren<Animator>());
        }
    }
}

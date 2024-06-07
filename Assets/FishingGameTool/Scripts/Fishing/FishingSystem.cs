using FishingGameTool.Fishing.BaitData;
using FishingGameTool.Fishing.CatchProbability;
using FishingGameTool.Fishing.Float;
using FishingGameTool.Fishing.Line;
using FishingGameTool.Fishing.LootData;
using FishingGameTool.Fishing.Rod;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using FishingGameTool.CustomAttribute;
using UnityEngine.InputSystem;

namespace FishingGameTool.Fishing
{
    public enum LootCatchType
    {
        SpawnItem,
        InvokeEvent
    };

    [AddComponentMenu("Fishing Game Tool/Fishing System")]
    public class FishingSystem : MonoBehaviour
    {
        [System.Serializable]
        public class LootCatchEvent
        {
            [InfoBox("This UnityEvent passes a prefab from LootData as an argument of type GameObject.")]
            [InfoBox("This feature is under development. In version 1.0, it does not work.")]
            public int _invokeCalls = 1;
            public UnityEvent _event;
        }

        [System.Serializable]
        public class AdvancedSettings
        {
            [ReadOnlyField]
            public FishingLootData _caughtLootData;

            [InfoBox("Custom Catch Probability Data.")]
            public CatchProbabilityData _catchProbabilityData;

            public bool _caughtLoot = false;
            public float _returnSpeedWithoutLoot = 3f;
            [InfoBox("The time interval in seconds between consecutive checks to determine if the catch has been successfully caught.")]
            public float _catchCheckInterval = 1f;
            [ReadOnlyField]
            public float _lootWeight;
        }

        [BetterHeader("Fishing Settings", 20), Space]
        public FishingRod _fishingRod;

        [InfoBox("LayerMask used to determine on which layer fishing is allowed.")]
        public LayerMask _fishingLayer;
        public FishingBaitData _bait;
        private FishingBaitData _baitForever;


        [Space ,BetterHeader("Loot Settings", 20) ,InfoBox("Select the method of handling loot catching:\n\n- \"SpawnItem\": Creates a game object " +
            "upon successfully catching the loot.\n- \"InvokeEvent\": Triggers a designated event upon successfully catching the loot.")]
        public LootCatchType _lootCatchType;

        [HideInInspector]
        public bool _showCatchEvent = false;

        [ShowVariable("_showCatchEvent")]
        public LootCatchEvent _lootCatchEvent;

        [Space, BetterHeader("Cast And Attract Settings", 20)]
        public GameObject _fishingFloatPrefab;
        public float _maxCastForce = 20f;
        public float _forceChargeRate = 4f;
        [ReadOnlyField]
        public float _currentCastForce;
        public float _spawnFloatDelay = 0.3f;

        [InfoBox("Minimum distance required for successfully picking up or catching a target (e.g., fish or object).")]
        public float _catchDistance = 3.5f;

        [Space ,AddButton("Advanced Settings", "_showAdvancedSettings")]
        public bool _showAdvancedSettings = false;

        [ShowVariable("_showAdvancedSettings")]
        public AdvancedSettings _advanced;

        [HideInInspector]
        public bool _attractInput;
        [HideInInspector]
        public bool _castInput;
        [HideInInspector]
        public bool _castFloat = false;

        [SerializeField]
        public InputActionReference _leftAction;
        [SerializeField]
        public InputActionReference _rightAction;

        #region PRIVATE VARIABLES

        private float _catchCheckIntervalTimer;
        private float _randomSpeedChangerTimer = 2f;
        private float _randomSpeedChanger = 1f;
        private float _finalSpeed;

        FishingFloatPathfinder _fishingFloatPathfinder = new FishingFloatPathfinder();

        #endregion

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            if (_lootCatchType == LootCatchType.InvokeEvent)
                _showCatchEvent = true;
            else
                _showCatchEvent = false;
        }

#endif

        static InputAction GetInputAction(InputActionReference actionReference)
        {
        #pragma warning disable IDE0031 // Use null propagation -- Do not use for UnityEngine.Object types
            return actionReference != null ? actionReference.action : null;
        #pragma warning restore IDE0031
        }

        private void Awake()
        {
            _catchCheckIntervalTimer = _advanced._catchCheckInterval;
            _baitForever = _bait;

            var leftAction = GetInputAction(_leftAction);
            if (leftAction != null)
            {
                leftAction.started += HandleCastInput;
            }

            var rightAction = GetInputAction(_rightAction);
            if(rightAction != null)
            {
                rightAction.started += HandleAttractInput;
            }

        }

        private void Update()
        {
            if (_fishingRod != null)
            {
                AttractFloat();
                CastFloat();
            }

            //HandleInput(); //Should not be needed anymore, i hope :))
        }

        #region AttractFloat

        private void AttractFloat()
        {
            if (_fishingRod._fishingFloat == null)
                return;

            SubstrateType substrateType = _fishingRod._fishingFloat.GetComponent<FishingFloat>().CheckSurface(_fishingLayer);

            if (substrateType == SubstrateType.Water)
                _advanced._caughtLoot = CheckingLoot(_advanced._caughtLoot, _bait, _advanced._catchProbabilityData, transform.position, _fishingRod._fishingFloat.position);
            
            LineLengthLimitation(_fishingRod._fishingFloat.gameObject, transform.position, _fishingRod._lineStatus, substrateType);

            if (_attractInput && substrateType == SubstrateType.InAir && !_advanced._caughtLoot)
            {
                Destroy(_fishingRod._fishingFloat.gameObject);
                _fishingRod._fishingFloat = null;

                return;
            }
            else if (_attractInput && substrateType == SubstrateType.Land && !_advanced._caughtLoot)
            {
                float distance = Vector3.Distance(transform.position, _fishingRod._fishingFloat.position);
                float speed = _advanced._returnSpeedWithoutLoot * 120f * Time.deltaTime;

                Vector3 direction = (transform.position - _fishingRod._fishingFloat.position).normalized;

                Rigidbody fishingFloatRB = _fishingRod._fishingFloat.GetComponent<Rigidbody>();
                fishingFloatRB.velocity = direction * speed;

                if (distance <= _catchDistance)
                {
                    Destroy(_fishingRod._fishingFloat.gameObject);
                    _fishingRod._fishingFloat = null;

                    return;
                }
            }
            else if (_attractInput && substrateType == SubstrateType.Water && !_advanced._caughtLoot)
            {
                float distance = Vector3.Distance(transform.position, _fishingRod._fishingFloat.position);

                 _fishingFloatPathfinder.FloatBehavior(null, _fishingRod._fishingFloat, transform.position, _fishingRod._lineStatus._maxLineLength, 
                    _advanced._returnSpeedWithoutLoot, _attractInput, _fishingLayer);

                if (distance <= _catchDistance)
                {
                    Destroy(_fishingRod._fishingFloat.gameObject);
                    _fishingRod._fishingFloat = null;

                    return;
                }
            }
            else if(_advanced._caughtLoot && _fishingRod._fishingFloat != null)
            {
                _fishingRod.LootCaught(_advanced._caughtLoot);

                while (_advanced._caughtLootData == null)
                {
                    List<FishingLootData> lootDataList = _fishingRod._fishingFloat.GetComponent<FishingFloat>().GetLootDataFormWaterObject();
                    _advanced._caughtLootData = ChooseFishingLoot(_bait, lootDataList);

                    if(_advanced._caughtLootData != null)
                    {
                        float lootWeight = Random.Range(_advanced._caughtLootData._weightRange._minWeight, _advanced._caughtLootData._weightRange._maxWeight);
                        _advanced._lootWeight = lootWeight;

                        _bait = _baitForever;
                    }
                }

                AttractWithLoot(_advanced._caughtLootData, _fishingRod._fishingFloat, transform.position, _fishingLayer, _attractInput, _advanced._lootWeight, _fishingRod);

                if (_attractInput && _fishingRod._fishingFloat != null)
                {
                    float distance = Vector3.Distance(transform.position, _fishingRod._fishingFloat.position);

                    if (distance <= _catchDistance)
                    {
                        GrabLoot(_advanced._caughtLootData, _fishingRod._fishingFloat.position, transform.position);
                        Destroy(_fishingRod._fishingFloat.gameObject);
                        _fishingRod._fishingFloat = null;
                        _advanced._caughtLoot = false;
                        _advanced._caughtLootData = null;
                        _fishingRod.FinishFishing();

                        _fishingFloatPathfinder.ClearPathData();

                        return;
                    }
                }
            }
        }

        #endregion

        #region LineLengthLimitation
        private void LineLengthLimitation(GameObject fishingFloat, Vector3 transformPosition, FishingLineStatus fishingLineStatus, SubstrateType substrateType)
        {
            if (fishingLineStatus._currentLineLength > fishingLineStatus._maxLineLength && substrateType != SubstrateType.Water)
            {
                Vector3 fishingFloatPosition = fishingFloat.transform.position;
                Vector3 direction = (transformPosition - fishingFloatPosition).normalized;

                Rigidbody fishingFloatRB = fishingFloat.GetComponent<Rigidbody>();

                float speed = (fishingLineStatus._currentLineLength - fishingLineStatus._maxLineLength) / Time.deltaTime;
                float maxSpeed = 5f;
                float clampedSpeed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);

                fishingFloatRB.velocity = direction * clampedSpeed;
            }
        }

        #endregion

        #region AttractWithLoot

        private void AttractWithLoot(FishingLootData lootData, Transform fishingFloatTransform, Vector3 transformPosition, LayerMask fishingLayer, bool attractInput, 
            float lootWeight, FishingRod fishingRod)
        {
            float lootSpeed = CalculateLootSpeed(lootData, lootWeight);
            float attractSpeed = CalculateAttractSpeed(fishingRod, attractInput, lootWeight, (int)lootData._lootTier);
            _finalSpeed = Mathf.Lerp(_finalSpeed, attractInput == true ? CalculateFinalAttractSpeed(lootSpeed, attractSpeed, lootData) : lootSpeed, 3f * Time.deltaTime);

            _fishingFloatPathfinder.FloatBehavior(lootData ,fishingFloatTransform, transformPosition, fishingRod._lineStatus._maxLineLength, _finalSpeed, attractInput, fishingLayer);

            //Debug.Log("Loot Speed: " + lootSpeed + " | Attract Speed: " + attractSpeed + " | Final Speed: " + _finalSpeed);
        }

        private float CalculateLootSpeed(FishingLootData lootData, float lootWeight)
        {
            float[] speedMultipliersByTier = { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f };
            float baseSpeed = 1.4f;

            int tier = (int)lootData._lootTier;

            _randomSpeedChangerTimer -= Time.deltaTime;
            if (_randomSpeedChangerTimer < 0)
            {
                _randomSpeedChanger = Random.Range(1f, 3f);
                _randomSpeedChangerTimer = Random.Range(2f, 4f);
            }

            float lootSpeed = (baseSpeed + lootWeight * 0.1f * speedMultipliersByTier[tier]) * _randomSpeedChanger;
            return lootSpeed;
        }

        private float CalculateAttractSpeed(FishingRod fishingRod, bool attractInput, float lootWeight, int lootTier)
        {
            FishingLineStatus fishingLineStatus = fishingRod.CalculateLineLoad(_attractInput, lootWeight, lootTier);
            float attractionSpeed = fishingLineStatus._attractFloatSpeed;

            return attractionSpeed;
        }

        private float CalculateFinalAttractSpeed(float lootSpeed, float attractSpeed, FishingLootData lootData)
        {
            int tier = (int)lootData._lootTier;
            float[] speedFactorByTier = { 1.2f, 1.0f, 0.8f, 0.6f, 0.5f };

            float finalAttractSpeed = (attractSpeed - lootSpeed) * speedFactorByTier[tier];
            finalAttractSpeed = finalAttractSpeed < 2f ? 2f : finalAttractSpeed;

            return finalAttractSpeed;
        }

        #endregion

        #region GrabLoot
        private void GrabLoot(FishingLootData lootData, Vector3 fishingFloatPosition, Vector3 transformPosition)
        {
            switch (_lootCatchType)
            {
                case LootCatchType.SpawnItem:

                    if (lootData._lootPrefab == null)
                    {
                        Debug.LogError("No loot prefab added!");
                        return;
                    }

                    SpawnLootItem(lootData._lootPrefab, fishingFloatPosition, transformPosition);

                    return;

                case LootCatchType.InvokeEvent:

                    Debug.LogWarning("This feature is under development. In version 1.0, it does not work.");

                    return;
            }
        }

        private void SpawnLootItem(GameObject lootPrefab, Vector3 fishingFloatPosition, Vector3 transformPosition)
        {
            Vector3 direction = ((transformPosition - fishingFloatPosition) + (Vector3.up * 3f)).normalized;
            Vector3 spawnPosition = fishingFloatPosition + new Vector3(0f, 1f, 0f);

            GameObject spawnedLootPrefab = Instantiate(lootPrefab, spawnPosition, Quaternion.identity);

            float distance = Vector3.Distance(fishingFloatPosition, transformPosition);
            float desiredTime = 0.8f;

            float force = distance / desiredTime;

            spawnedLootPrefab.GetComponent<Rigidbody>().AddForce(direction * force, ForceMode.Impulse);
        }

        #endregion

        #region CheckingLoot

        private bool CheckingLoot(bool caughtLoot, FishingBaitData baitData, CatchProbabilityData catchProbabilityData, 
            Vector3 transformPosition, Vector3 fishingFloatPosition)
        {
            if (caughtLoot)
                return true;

            bool caught = false;

            _catchCheckIntervalTimer -= Time.deltaTime;

            if (_catchCheckIntervalTimer <= 0)
            {
                caught = CheckLootIsCatch(baitData, catchProbabilityData, transformPosition, fishingFloatPosition);
                _catchCheckIntervalTimer = _advanced._catchCheckInterval;
            }

            return caught;
        }

        private bool CheckLootIsCatch(FishingBaitData baitData, CatchProbabilityData catchProbabilityData, 
            Vector3 transformPosition, Vector3 fishingFloatPosition)
        {
            float distance = Vector3.Distance(transformPosition, fishingFloatPosition);
            float minSafeFishingDistanceFactor = 10f;

            int chanceVal = Random.Range(1, 100);

            int commonProbability = 5;
            int uncommonProbability = 12;
            int rareProbability = 22;
            int epicProbability = 35;
            int legendaryProbability = 45;

            if (catchProbabilityData != null)
            {
                commonProbability = catchProbabilityData._commonProbability;
                uncommonProbability = catchProbabilityData._uncommonProbability;
                rareProbability = catchProbabilityData._rareProbability;
                epicProbability = catchProbabilityData._epicProbability;
                legendaryProbability = catchProbabilityData._legendaryProbability;
                minSafeFishingDistanceFactor = catchProbabilityData._minSafeFishingDistanceFactor;
            }

            commonProbability = CalculateProbabilityValueByCastDistance(commonProbability, distance, minSafeFishingDistanceFactor);
            uncommonProbability = CalculateProbabilityValueByCastDistance(uncommonProbability, distance, minSafeFishingDistanceFactor);
            rareProbability = CalculateProbabilityValueByCastDistance(rareProbability, distance, minSafeFishingDistanceFactor);
            epicProbability = CalculateProbabilityValueByCastDistance(epicProbability, distance, minSafeFishingDistanceFactor);
            legendaryProbability = CalculateProbabilityValueByCastDistance(legendaryProbability, distance, minSafeFishingDistanceFactor);

            if (baitData == null)
            {
                if (chanceVal <= commonProbability)
                    return true;
                else
                    return false;
            }
            else
            {
                switch (baitData._baitTier)
                {
                    case BaitTier.Uncommon:

                        if (chanceVal <= uncommonProbability)
                            return true;
                        else
                            return false;

                    case BaitTier.Rare:

                        if (chanceVal <= rareProbability)
                            return true;
                        else
                            return false;

                    case BaitTier.Epic:

                        if (chanceVal <= epicProbability)
                            return true;
                        else
                            return false;

                    case BaitTier.Legendary:

                        if (chanceVal <= legendaryProbability)
                            return true;
                        else
                            return false;
                }
            }

            return false;
        }

        private static int CalculateProbabilityValueByCastDistance(float probability, float distance, float minSafeFishingDistanceFactor)
        {
            float minValue = 0.3f;
            float maxValue = 1f;

            float x = Mathf.InverseLerp(0, minSafeFishingDistanceFactor, distance);
            float value = Mathf.Lerp(minValue, maxValue, x);

            probability = probability * value;

            return (int)probability;
        }

        #endregion

        #region ChooseFishingLoot

        private FishingLootData ChooseFishingLoot(FishingBaitData baitData, List<FishingLootData> lootDataList)
        {
            for (int i = 0; i < lootDataList.Count; i++)
            {
                FishingLootData temp = lootDataList[i];
                int randomIndex = Random.Range(i, lootDataList.Count);
                lootDataList[i] = lootDataList[randomIndex];
                lootDataList[randomIndex] = temp;
            }

            float totalRarity = CalculateTotalRarity(lootDataList);
            List<float> lootRarityList = CalculatePercentageRarity(lootDataList, totalRarity);

            int baitTier = 0;

            if (baitData != null)
                baitTier = (int)baitData._baitTier + 1;

            float chanceVal = Random.Range(1f, 100f);
            float addedRarity = 0f;

            for(int i = 0; i < lootRarityList.Count; i++)
            {
                addedRarity += lootRarityList[i];

                if (chanceVal <= addedRarity)
                {
                    if (baitTier >= (int)lootDataList[i]._lootTier)
                        return lootDataList[i];
                    else
                        return null;
                }
            }

            return null;
        }

        private float CalculateTotalRarity(List<FishingLootData> lootDataList)
        {
            float totalRarity = 0;

            foreach (var lootData in lootDataList)
            {
                totalRarity += lootData._lootRarity;
            }

            return totalRarity;
        }

        private List<float> CalculatePercentageRarity(List<FishingLootData> lootDataList, float totalRarity)
        {
            List<float> lootRarityList = new List<float>();

            foreach (var lootData in lootDataList)
            {
                float percentageRarity = (lootData._lootRarity / totalRarity) * 100f;
                lootRarityList.Add(percentageRarity);

                //Debug.Log(lootData + " - Rarity Percentage: " + percentageRarity + "%");
            }

            return lootRarityList;
        }

        #endregion

        #region CastFloat

        private void CastFloat()
        {
            if (_fishingRod._fishingFloat != null || _castFloat || _fishingRod._lineStatus._isLineBroken)
                return;

            if (_castInput)
            {
                _currentCastForce = CalculateCastForce(_currentCastForce, _maxCastForce, _forceChargeRate);
            }
            else if (!_castInput && _currentCastForce != 0f)
            {
                Vector3 spawnPoint = _fishingRod._line._lineAttachment.position;
                Vector3 castDirection = transform.forward + Vector3.up;

                StartCoroutine(CastingDelay(_spawnFloatDelay, castDirection, spawnPoint, _currentCastForce, _fishingFloatPrefab));

                _currentCastForce = 0f;
            }
        }

        private IEnumerator CastingDelay(float delay, Vector3 castDirection, Vector3 spawnPoint, float castForce, GameObject fishingFloatPrefab)
        {
            _castFloat = true;

            yield return new WaitForSeconds(delay);
            _fishingRod._fishingFloat = Cast(castDirection, spawnPoint, castForce, fishingFloatPrefab);
            _castFloat = false;
        }

        private Transform Cast(Vector3 castDirection, Vector3 spawnPoint, float castForce, GameObject fishingFloatPrefab)
        {
            GameObject spawnedFishingFloat = Instantiate(fishingFloatPrefab, spawnPoint, Quaternion.identity);
            spawnedFishingFloat.GetComponent<Rigidbody>().AddForce(castDirection * castForce, ForceMode.Impulse);

            return spawnedFishingFloat.transform;
        }

        private float CalculateCastForce(float currentCastForce, float maxCastForce, float forceChargeRate)
        {
            currentCastForce += forceChargeRate * Time.deltaTime;
            currentCastForce = currentCastForce > maxCastForce ? maxCastForce : currentCastForce;

            return currentCastForce;
        }

        #endregion

        public void ForceStopFishing()
        {
            Destroy(_fishingRod._fishingFloat.gameObject);
            _fishingRod._fishingFloat = null;
            _advanced._caughtLoot = false;
            _advanced._caughtLootData = null;
            _fishingRod.FinishFishing();
            _fishingFloatPathfinder.ClearPathData();
        }

        public void AddBait(FishingBaitData baitData)
        {
            if(_bait == null)
                _bait = baitData;
            else
            {
                Vector3 spawnPos = transform.position + transform.forward + Vector3.up;
                Instantiate(_bait._baitPrefab, spawnPos, Quaternion.identity);

                _bait = baitData;
            }
        }

        public void AddCustomCatchProbabilityData(CatchProbabilityData catchProbabilityData)
        {
            _advanced._catchProbabilityData = catchProbabilityData;
        }

        public void FixFishingLine()
        {
            if (_fishingRod._lineStatus._isLineBroken)
                _fishingRod._lineStatus._isLineBroken = false;
        }

        private void HandleInput()
        {
            _attractInput = Input.GetButton("Fire2");// current: Right click, TODO: Controller "pulling in"
            _castInput = Input.GetButton("Fire1"); //current: Left click, TODO: Controller "throwing out"
        }

        private void HandleCastInput(InputAction.CallbackContext context)
        {
            _castInput = true;
        }

        private void HandleAttractInput(InputAction.CallbackContext context)
        {
            _attractInput = true;
        }
    }
}
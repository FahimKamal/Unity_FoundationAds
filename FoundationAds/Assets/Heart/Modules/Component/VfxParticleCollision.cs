using System;
using System.Collections.Generic;
using System.Linq;
using Pancake.Sound;
using Sirenix.OdinInspector;
using UnityEngine;
#if PANCAKE_ROUTER
using VitalRouter;
#endif

namespace Pancake.Component
{
    [EditorIcon("icon_default")]
    public class VfxParticleCollision : GameComponent
    {
        public StringConstant type;
        [field: SerializeField] public ParticleSystem PS { get; private set; }
        public Dictionary<int, int> numberParticleMap;

        [SerializeField] private bool enabledSound;

        [SerializeField, AudioPickup, ShowIf(nameof(enabledSound)), Indent]
        private AudioId audioCollision;

        private int _segmentValue;
        private bool _flag;
        private Action<GameObject> _returnEvent;
        private Func<bool> _isFxInstanceEmpty;

        public void Init(int value, Action<GameObject> returnEvent, Func<bool> isFxInstanceEmpty)
        {
            _returnEvent = returnEvent;
            _isFxInstanceEmpty = isFxInstanceEmpty;
            _flag = false;

            var sorted = numberParticleMap.OrderByDescending(x => x.Key).ToList();
            int maxParticle = sorted.First().Value;
            foreach (var particle in sorted)
            {
                if (value >= particle.Key)
                {
                    maxParticle = particle.Value;
                    break;
                }
            }

            var main = PS.main;
            main.maxParticles = maxParticle;
            _segmentValue = value / maxParticle;
        }

        private void OnParticleCollision(GameObject particle)
        {
#if PANCAKE_ROUTER
            Router.Default.PublishAsync(new UpdateCurrencyWithValueCommand(type.Value, _segmentValue));
#endif
            if (enabledSound) audioCollision.Play();
        }

        protected void Update()
        {
            if (PS.particleCount > 0) return;

            if (!_flag)
            {
                _flag = true;
                // remove external force module
                ParticleSystem.ExternalForcesModule externalForcesModule = PS.externalForces;
                externalForcesModule.RemoveAllInfluences();
                externalForcesModule.enabled = false;
                _returnEvent.Invoke(gameObject);
#if PANCAKE_ROUTER
                if (_isFxInstanceEmpty.Invoke()) Router.Default.PublishAsync(new UpdateCurrencyCommand(type.Value));
#endif
            }
        }
    }
}
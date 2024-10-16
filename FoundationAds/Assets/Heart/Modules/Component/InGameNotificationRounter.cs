using Pancake.Common;
using Pancake.Pools;
using UnityEngine;
#if PANCAKE_ROUTER
using VitalRouter;
#endif

namespace Pancake.Component
{
#if PANCAKE_ROUTER
    [Routes]
#endif
    [EditorIcon("icon_default")]
    public partial class InGameNotificationRouter : GameComponent
    {
        [SerializeField] private GameObject notificationPrefab;
        [SerializeField] private RectTransform root;

#if PANCAKE_ROUTER
        private void Awake() { MapTo(Router.Default); }

        public void OnSpawn(SpawnInGameNotiCommand cmd)
        {
            var instance = notificationPrefab.Request<InGameNotification>();
            instance.transform.SetParent(root, false);
            instance.transform.localScale = Vector3.one;
            var rectTransform = instance.transform.GetComponent<RectTransform>();
            rectTransform.SetLocalPositionZ(0);
            rectTransform.SetAnchoredPositionY(-444);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, root.rect.width - 100);
            instance.Show(cmd.LocaleText);
        }
#endif
    }
}
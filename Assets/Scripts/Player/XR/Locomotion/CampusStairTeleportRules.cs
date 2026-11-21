using UnityEngine;
using CampusEmergency.Player;

namespace CampusEmergency.Player.XR
{
    /// <summary>Campus stair bounds and continuous supported travel, separate from fire and inventory rules.</summary>
    public static class CampusStairTeleportRules
    {
        public static bool InStairEnvelope(Vector3 point)
        {
            float x = Mathf.Abs(point.x);
            return x >= 13f && x <= 18.2f && Mathf.Abs(point.z) <= 9.2f && point.y >= -.2f && point.y <= 11.2f;
        }
        public static bool IsAllowedPoint(Vector3 point)
        {
            if (!InStairEnvelope(point)) return true;
            // The authored front/back FloorSlab extensions are not stair landings.
            // Keep Landing (+Z), HalfLanding (-Z), and the supported flights between them.
            return point.z >= -4.18f && point.z <= 4.18f;
        }
        public static bool IsConnected(Vector3 from, Vector3 to, LayerMask mask, out string reason)
        {
            reason = string.Empty;
            if (!IsAllowedPoint(to))
                return Reject(LanguageSelectionUI.T("Choose a stair or marked landing", "请选择楼梯或正常平台，外围地板不可传送", "請選擇樓梯或正常平台，外圍地板不可傳送"), out reason);
            if (!InStairEnvelope(from) && !InStairEnvelope(to)) return true;
            if (Mathf.Abs(from.y - to.y) > 2.05f)
                return Reject(LanguageSelectionUI.T("Use the half landing before the next floor", "请经半层平台分段上下楼，不能直接跳到另一楼层", "請經半層平台分段上下樓，不能直接跳到另一樓層"), out reason);
            Vector3 delta = to - from;
            int count = Mathf.Max(1, Mathf.CeilToInt(new Vector2(delta.x, delta.z).magnitude / .18f));
            for (int i = 1; i <= count; i++)
            {
                Vector3 sample = Vector3.Lerp(from, to, (float)i / count);
                if (!IsAllowedPoint(sample) || !Physics.Raycast(sample + Vector3.up * .65f, Vector3.down,
                    out var floor, 1.3f, mask, QueryTriggerInteraction.Ignore)
                    || floor.collider.GetComponent<CampusTeleportSurface>() == null
                    || !IsAllowedPoint(floor.point) || Mathf.Abs(floor.point.y - sample.y) > .62f)
                    return Reject(LanguageSelectionUI.T("Follow the connected stair flight and landing", "请沿相连梯段和平台移动，不能跨越楼梯空隙", "請沿相連梯段和平台移動，不能跨越樓梯空隙"), out reason);
            }
            // Catch rail/wall shortcuts without sweeping the capsule into normal risers.
            if (delta.sqrMagnitude > .0001f && Physics.SphereCast(from + Vector3.up * .9f, .12f,
                delta.normalized, out _, delta.magnitude, mask, QueryTriggerInteraction.Ignore))
                return Reject(LanguageSelectionUI.T("Stair route is obstructed", "楼梯通路被栏杆、墙或门阻挡，请选择相邻平台", "樓梯通路被欄杆、牆或門阻擋，請選擇相鄰平台"), out reason);
            return true;
        }
        private static bool Reject(string value, out string reason) { reason = value; return false; }
    }
}

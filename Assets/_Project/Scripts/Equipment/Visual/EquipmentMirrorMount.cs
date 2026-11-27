using UnityEngine;

namespace ProjectC.Equipment.Visual
{
    public class EquipmentMirrorMount : MonoBehaviour
    {
        public HumanBodyBones bone = HumanBodyBones.LastBone;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }
}

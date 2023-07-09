using UnityEngine;

public enum Axis {XPositive, YPositive, ZPositive, XNegative, YNegative, ZNegative, None};

public static class EnumExtensions {
    public static Vector3 GetAxis(this Axis axis) {
		switch(axis) {
			case Axis.None:
			return Vector3.zero;
			case Axis.XPositive:
			return Vector3.right;
			case Axis.XNegative:
			return Vector3.left;
			case Axis.YPositive:
			return Vector3.up;
			case Axis.YNegative:
			return Vector3.down;
			case Axis.ZPositive:
			return Vector3.forward;
			case Axis.ZNegative:
			return Vector3.back;
		}
		return Vector3.zero;
    }
}
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MotionEvent : MonoBehaviour {

	public abstract void Callback(MotionEditor editor);
	
}
#endif
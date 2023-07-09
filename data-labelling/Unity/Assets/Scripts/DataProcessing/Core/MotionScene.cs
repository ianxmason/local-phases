#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]

public class MotionScene : MonoBehaviour {

    public MotionFile File = null;
    public Sequence[] Sequences = new Sequence[0];

    void Update() {
        //Validation
        if(File == null) {
            File = GetComponentInParent<MotionFile>();
        }
        if(Sequences.Length == 0) {
            AddSequence();
        }
        //
    }

    public void SetActive(bool value) {
        gameObject.SetActive(value);
    }

    public bool IsActive() {
        return gameObject.activeSelf;
    }

    public void SetIndex(int index) {
        transform.name = "Scene"+(index+1);
        transform.SetSiblingIndex(index);
    }

    public int GetIndex() {
        return transform.GetSiblingIndex();
    }

    public void AddSequence() {
        ArrayExtensions.Add(ref Sequences, new Sequence(1, File.Data.GetTotalFrames()));
    }

    public void RemoveSequence() {
        if(Sequences.Length > 1) {
            ArrayExtensions.Shrink(ref Sequences);
        }
    }

}
#endif
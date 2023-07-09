#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MotionFile : MonoBehaviour {
    public MotionData Data;
    public MotionScene[] Scenes = new MotionScene[0];
    public MotionScene ActiveScene = null;

    void Update() {
        //Validation
        for(int i=0; i<Scenes.Length; i++) {
            if(Scenes[i] == null) {
                Debug.Log("Removing missing scene.");
                ArrayExtensions.RemoveAt(ref Scenes, i);
                i--;
            }
        }
        if(Scenes.Length == 0) {
            Debug.Log("Creating scene.");
            AddScene();
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
        transform.SetSiblingIndex(index);
    }

    public int GetIndex() {
        return transform.GetSiblingIndex();
    }

    public void AddScene() {
        ArrayExtensions.Add(ref Scenes, new GameObject("Scene"+(Scenes.Length+1)).AddComponent<MotionScene>());
        Scenes.Last().transform.SetParent(transform);
        Scenes.Last().File = this;
        Scenes.Last().SetActive(false);
        Scenes.Last().AddSequence();
    }

    public void RemoveScene(MotionScene scene) {
        if(Scenes.Length > 1) {
            if(ArrayExtensions.Contains(ref Scenes, scene)) {
                ArrayExtensions.Remove(ref Scenes, scene);
                Utility.Destroy(scene.gameObject);
                for(int i=0; i<Scenes.Length; i++) {
                    Scenes[i].SetIndex(i);
                }
            }
        }
    }

    public void LoadScene(int index) {
        if(ActiveScene != GetScene(index)) {
            for(int i=0; i<Scenes.Length; i++) {
                Scenes[i].SetActive(Scenes[i].GetIndex() == index);
            }
            ActiveScene = GetScene(index);
        }
    }

    public MotionScene GetScene(int index) {
        return index < Scenes.Length ? Scenes[index] : null;
    }

    public MotionScene GetActiveScene() {
        if(ActiveScene == null) {
            LoadScene(0);
        }
        return ActiveScene;
    }

    public string[] GetSceneNames() {
        string[] names = new string[Scenes.Length];
        for(int i=0; i<names.Length; i++) {
            names[i] = Scenes[i] == null ? "Missing" : Scenes[i].name;
        }
        return names;
    }
}
#endif
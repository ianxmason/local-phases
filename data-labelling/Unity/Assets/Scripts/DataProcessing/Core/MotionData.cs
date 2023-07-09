#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class MotionData : ScriptableObject {

	public enum AXIS {XPositive, YPositive, ZPositive, XNegative, YNegative, ZNegative};

	public Hierarchy Source = null;
	public Frame[] Frames = new Frame[0];
	public Module[] Modules = new Module[0];

	public float Framerate = 1f;
	public float Scaling = 1f;
	public AXIS MirrorAxis = AXIS.XPositive;
	public int[] Symmetry = new int[0];
	public bool Export = false;

	public float GetTotalTime() {
		return GetTotalFrames() / Framerate;
	}

	public int GetTotalFrames() {
		return Frames.Length;
	}

	public Frame GetFirstFrame() {
		return Frames[0];
	}

	public Frame GetLastFrame() {
		return Frames[Frames.Length-1];
	}

	public Frame GetFrame(int index) {
		if(index < 1 || index > GetTotalFrames()) {
			Debug.Log("Please specify an index between 1 and " + GetTotalFrames() + ".");
			return null;
		}
		return Frames[index-1];
	}

	public Frame GetFrame(float time) {
		if(time < 0f || time > GetTotalTime()) {
			Debug.Log("Please specify a time between 0 and " + GetTotalTime() + ".");
			return null;
		}
		return GetFrame(Mathf.Min(Mathf.RoundToInt(time * Framerate) + 1, GetTotalFrames()));
	}

	public Frame[] GetFrames(int start, int end) {
		if(start < 1 || end > GetTotalFrames()) {
			Debug.Log("Please specify indices between 1 and " + GetTotalFrames() + ".");
			return null;
		}
		int count = end-start+1;
		Frame[] frames = new Frame[count];
		for(int i=start; i<=end; i++) {
			frames[i-start] = Frames[i-1];
		}
		return frames;
	}

	public Frame[] GetFrames(float start, float end) {
		if(start < 0f || end > GetTotalTime()) {
			Debug.Log("Please specify times between 0 and " + GetTotalTime() + ".");
			return null;
		}
		return GetFrames(GetFrame(start).Index, GetFrame(end).Index);
	}

	public float GetDeltaTime() {
		return 1f / Framerate;
	}

	//Returns absolute timestamps around frame at framerate steps by frame padding.
	public float[] SimulateTimestamps(Frame frame, int padding) {
		float step = 1f/Framerate;
		float start = frame.Timestamp - padding*step;
		float[] timestamps = new float[2*padding+1];
		for(int i=0; i<timestamps.Length; i++) {
			timestamps[i] = start + i*step;
		}
		return timestamps;
	}

	//Returns absolute timestamps around frame at framerate steps by frame padding.
	public float[] SimulateTimestamps(Frame frame, int pastPadding, int futurePadding) {
		float step = 1f/Framerate;
		float start = frame.Timestamp - pastPadding*step;
		float[] timestamps = new float[Mathf.RoundToInt((float)(pastPadding+futurePadding)/step)+1];
		for(int i=0; i<timestamps.Length; i++) {
			timestamps[i] = start + i*step;
		}
		return timestamps;
	}

	//Returns absolute timestamps around frame at framerate steps by time padding.
	public float[] SimulateTimestamps(Frame frame, float padding) {
		float step = 1f/Framerate;
		float start = frame.Timestamp - padding;
		float[] timestamps = new float[2*Mathf.RoundToInt(padding/step)+1];
		for(int i=0; i<timestamps.Length; i++) {
			timestamps[i] = start + i*step;
		}
		return timestamps;
	}

	//Returns absolute timestamps around frame at framerate steps by time padding.
	public float[] SimulateTimestamps(Frame frame, float pastPadding, float futurePadding) {
		float step = 1f/Framerate;
		float start = frame.Timestamp - pastPadding;
		float[] timestamps = new float[Mathf.RoundToInt((pastPadding+futurePadding)/step)+1];
		for(int i=0; i<timestamps.Length; i++) {
			timestamps[i] = start + i*step;
		}
		return timestamps;
	}

	public void AddModule(Module.TYPE type) {
		if(System.Array.Find(Modules, x => x.Type() == type)) {
			Debug.Log("Module of type " + type.ToString() + " already exists.");
		} else {
			switch(type) {
				case Module.TYPE.Trajectory:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<TrajectoryModule>().Initialise(this));
				break;
				case Module.TYPE.Style:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<StyleModule>().Initialise(this));
				break;
				case Module.TYPE.Phase:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<PhaseModule>().Initialise(this));
				break;
				case Module.TYPE.Gait:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<GaitModule>().Initialise(this));
				break;
				case Module.TYPE.Contact:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<ContactModule>().Initialise(this));
				break;
				case Module.TYPE.LocalPhase:
				ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<LocalPhaseModule>().Initialise(this));
				break;
				default:
				Debug.Log("Module of type " + type.ToString() + " not considered.");
				return;
			}
			AssetDatabase.AddObjectToAsset(Modules[Modules.Length-1], this);
		}
	}

	public void AddExtraPhaseModule() {
		ArrayExtensions.Add(ref Modules, ScriptableObject.CreateInstance<PhaseModule>().Initialise(this));
		AssetDatabase.AddObjectToAsset(Modules[Modules.Length-1], this);
	}

	public void RemoveModule(Module.TYPE type) {
		Module module = GetModule(type);
		if(!module) {
			Debug.Log("Module of type " + type.ToString() + " does not exist.");
		} else {
			ArrayExtensions.Remove(ref Modules, module);
			Utility.Destroy(module);
		}
	}

	public Module GetModule(Module.TYPE type) {
		return System.Array.Find(Modules, x => x.Type() == type);
	}

	public Module[] GetModules(Module.TYPE type) {
		return System.Array.FindAll(Modules, x => x.Type() == type);
	}

	public Vector3 GetAxis(AXIS axis) {
		switch(axis) {
			case AXIS.XPositive:
			return Vector3.right;
			case AXIS.YPositive:
			return Vector3.up;
			case AXIS.ZPositive:
			return Vector3.forward;
			case AXIS.XNegative:
			return -Vector3.right;
			case AXIS.YNegative:
			return -Vector3.up;
			case AXIS.ZNegative:
			return -Vector3.forward;
			default:
			return Vector3.zero;
		}
	}

	public void DetectSymmetry() {
		Symmetry = new int[Source.Bones.Length];
		for(int i=0; i<Source.Bones.Length; i++) {
			string name = Source.Bones[i].Name;
			if(name.Contains("Left")) {
				int pivot = name.IndexOf("Left");
				Hierarchy.Bone bone = Source.FindBone(name.Substring(0, pivot)+"Right"+name.Substring(pivot+4));
				if(bone == null) {
					Debug.Log("Could not find mapping for " + name + ".");
				} else {
					Symmetry[i] = bone.Index;
				}
			} else if(name.Contains("Right")) {
				int pivot = name.IndexOf("Right");
				Hierarchy.Bone bone = Source.FindBone(name.Substring(0, pivot)+"Left"+name.Substring(pivot+5));
				if(bone == null) {
					Debug.Log("Could not find mapping for " + name + ".");
				} else {
					Symmetry[i] = bone.Index;
				}
			} else if(name.StartsWith("L") && char.IsUpper(name[1])) {
				Hierarchy.Bone bone = Source.FindBone("R"+name.Substring(1));
				if(bone == null) {
					Debug.Log("Could not find mapping for " + name + ".");
				} else {
					Symmetry[i] = bone.Index;
				}
			} else if(name.StartsWith("R") && char.IsUpper(name[1])) {
				Hierarchy.Bone bone = Source.FindBone("L"+name.Substring(1));
				if(bone == null) {
					Debug.Log("Could not find mapping for " + name + ".");
				} else {
					Symmetry[i] = bone.Index;
				}
			} else {
				Symmetry[i] = i;
			}
		}
	}

	public void SetSymmetry(int source, int target) {
		if(Symmetry[source] != target) {
			Symmetry[source] = target;
		}
	}

	[System.Serializable]
	public class Hierarchy {
		public Bone[] Bones;

		private string[] Names;

		public Hierarchy() {
			Bones = new Bone[0];
		}

		public void AddBone(string name, string parent) {
			ArrayExtensions.Add(ref Bones, new Bone(Bones.Length, name, parent));
		}

		public Bone FindBone(string name) {
			return System.Array.Find(Bones, x => x.Name == name);
		}

		public Bone FindBoneContains(string name) {
			return System.Array.Find(Bones, x => x.Name.Contains(name));
		}

		public string[] GetNames() {
			if(Names == null || Names.Length == 0) {
				Names = new string[Bones.Length];
				for(int i=0; i<Bones.Length; i++) {
					Names[i] = Bones[i].Name;
				}
			}
			return Names;
		}

		[System.Serializable]
		public class Bone {
			public int Index = -1;
			public string Name = "";
			public string Parent = "";
			public Vector3 Alignment = Vector3.zero;
			public bool Active = true;
			public Bone(int index, string name, string parent) {
				Index = index;
				Name = name;
				Parent = parent;
				Alignment = Vector3.zero;
				Active = true;
			}
		}
	}
}
#endif
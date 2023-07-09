#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public class Frame {
	public MotionData Data;
	public int Index;
	public float Timestamp;
	public Matrix4x4[] Local;
	public Matrix4x4[] World;

	public Frame(MotionData data, int index, float timestamp) {
		Data = data;
		Index = index;
		Timestamp = timestamp;
		Local = new Matrix4x4[Data.Source.Bones.Length];
		World = new Matrix4x4[Data.Source.Bones.Length];
	}

	public Frame GetPreviousFrame() {
		return Data.Frames[Mathf.Clamp(Index-2, 0, Data.Frames.Length-1)];
	}

	public Frame GetNextFrame() {
		return Data.Frames[Mathf.Clamp(Index, 0, Data.Frames.Length-1)];
	}

	public Frame GetFirstFrame() {
		return Data.Frames[0];
	}

	public Frame GetLastFrame() {
		return Data.Frames[Data.Frames.Length-1];
	}

	public Matrix4x4[] GetBoneTransformations(bool mirrored) {
		List<Matrix4x4> transformations = new List<Matrix4x4>();
		for(int i=0; i<World.Length; i++) {
			if(Data.Source.Bones[i].Active) {
				transformations.Add(GetBoneTransformation(i, mirrored));
			}
		}
		return transformations.ToArray();
	}

	public Matrix4x4 GetBoneTransformation(int index, bool mirrored) {
		return Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Data.Scaling * Vector3.one) * (mirrored ? World[Data.Symmetry[index]].GetMirror(Data.GetAxis(Data.MirrorAxis)) : World[index]) * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(Data.Source.Bones[index].Alignment), Vector3.one); //Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(mirrored ? Data.Source.Bones[Data.Symmetry[index]].Alignment : Data.Source.Bones[index].Alignment), Vector3.one);
	}

	public Vector3[] GetBoneVelocities(bool mirrored) {
		List<Vector3> velocities = new List<Vector3>();
		for(int i=0; i<World.Length; i++) {
			if(Data.Source.Bones[i].Active) {
				velocities.Add(GetBoneVelocity(i, mirrored));
			}
		}
		return velocities.ToArray();
	}

	public Vector3 GetBoneVelocity(int index, bool mirrored) {
		if(Index == 1) {
			return GetNextFrame().GetBoneVelocity(index, mirrored);
		} else {
			return (GetBoneTransformation(index, mirrored).GetPosition() - GetPreviousFrame().GetBoneTransformation(index, mirrored).GetPosition()) * Data.Framerate;
		}
	}

	public Vector3 GetBoneLocalVelocity(int index, int rootIndex, bool mirrored) {
		if(Index == 1) {
			return GetNextFrame().GetBoneLocalVelocity(index, rootIndex, mirrored);
		} else {
			return (GetBoneTransformation(index, mirrored).GetPosition().GetRelativePositionTo(GetBoneTransformation(rootIndex, mirrored)) -
			             GetPreviousFrame().GetBoneTransformation(index, mirrored).GetPosition().GetRelativePositionTo(
				         GetPreviousFrame().GetBoneTransformation(rootIndex, mirrored))) * Data.Framerate;
		}
	}

}
#endif
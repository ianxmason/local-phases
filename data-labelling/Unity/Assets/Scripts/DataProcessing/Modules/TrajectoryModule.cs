#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class TrajectoryModule : Module {

	public bool ShowPath = false;
	public MotionData.AXIS ForwardAxis = MotionData.AXIS.ZPositive;
	public LayerMask Ground = -1;

	public override TYPE Type() {
		return TYPE.Trajectory;
	}

	public override Module Initialise(MotionData data) {
		Data = data;
		Inspect = true;
		Visualise = true;
		return this;
	}

	public Trajectory GetTrajectory(Frame frame, bool mirrored) {
		StyleModule styleModule = Data.GetModule(Module.TYPE.Style) == null ? null : (StyleModule)Data.GetModule(Module.TYPE.Style);
		PhaseModule phaseModule = Data.GetModule(Module.TYPE.Phase) == null ? null : (PhaseModule)Data.GetModule(Module.TYPE.Phase);
		GaitModule gaitModule = Data.GetModule(Module.TYPE.Gait) == null ? null : (GaitModule)Data.GetModule(Module.TYPE.Gait);
		
		Trajectory trajectory = new Trajectory(12, styleModule == null ? new string[0] : styleModule.GetNames(), gaitModule == null ? new string[0] : gaitModule.GetNames());

		//Current
		trajectory.Points[6].SetTransformation(GetRootTransformation(frame, mirrored));
		trajectory.Points[6].SetVelocity(GetRootVelocity(frame, mirrored));
		trajectory.Points[6].SetSpeed(GetSpeed(frame, mirrored));
		trajectory.Points[6].Styles = styleModule == null ? new float[0] : styleModule.GetStyle(frame);
		trajectory.Points[6].Phase = phaseModule == null ? 0f : phaseModule.GetPhase(frame, mirrored);
		trajectory.Points[6].Gaits = gaitModule == null ? new float[0] : gaitModule.GetGait(frame);

		//Past
		for(int i=0; i<6; i++) {
			float delta = -1f + (float)i/6f;
			if(frame.Timestamp + delta < 0f) {
				float pivot = - frame.Timestamp - delta;
				float clamped = Mathf.Clamp(pivot, 0f, Data.GetTotalTime());
				float ratio = pivot == clamped ? 1f : Mathf.Abs(pivot / clamped);
				Frame reference = Data.GetFrame(clamped);
				trajectory.Points[i].SetPosition(GetRootPosition(Data.GetFirstFrame(), mirrored) - ratio * (GetRootPosition(reference, mirrored) - GetRootPosition(Data.GetFirstFrame(), mirrored)));
				trajectory.Points[i].SetRotation(GetRootRotation(reference, mirrored));
				trajectory.Points[i].SetVelocity(GetRootVelocity(reference, mirrored));
				trajectory.Points[i].SetSpeed(GetSpeed(reference, mirrored));
				trajectory.Points[i].Styles = styleModule == null ? new float[0] : styleModule.GetStyle(reference);
				trajectory.Points[i].Phase = phaseModule == null ? 0f : Mathf.Repeat(phaseModule.GetPhase(Data.GetFirstFrame(), mirrored) - Utility.PhaseUpdate(phaseModule.GetPhase(Data.GetFirstFrame(), mirrored), phaseModule.GetPhase(reference, mirrored)), 1f);
				trajectory.Points[i].Gaits = gaitModule == null ? new float[0] : gaitModule.GetGait(reference);
			} else {
				Frame previous = Data.GetFrame(Mathf.Clamp(frame.Timestamp + delta, 0f, Data.GetTotalTime()));
				trajectory.Points[i].SetTransformation(GetRootTransformation(previous, mirrored));
				trajectory.Points[i].SetVelocity(GetRootVelocity(previous, mirrored));
				trajectory.Points[i].SetSpeed(GetSpeed(previous, mirrored));
				trajectory.Points[i].Styles = styleModule == null ? new float[0] : styleModule.GetStyle(previous);
				trajectory.Points[i].Phase = phaseModule == null ? 0f : phaseModule.GetPhase(previous, mirrored);
				trajectory.Points[i].Gaits = gaitModule == null ? new float[0] : gaitModule.GetGait(previous);
			}
		}

		//Future
		for(int i=1; i<=5; i++) {
			float delta = (float)i/5f;
			if(frame.Timestamp + delta > Data.GetTotalTime()) {
				float pivot = 2f*Data.GetTotalTime() - frame.Timestamp - delta;
				float clamped = Mathf.Clamp(pivot, 0f, Data.GetTotalTime());
				float ratio = pivot == clamped ?1f : Mathf.Abs((Data.GetTotalTime() - pivot) / (Data.GetTotalTime() - clamped));
				Frame reference = Data.GetFrame(clamped);
				trajectory.Points[6+i].SetPosition(GetRootPosition(Data.GetLastFrame(), mirrored) - ratio * (GetRootPosition(reference, mirrored) - GetRootPosition(Data.GetLastFrame(), mirrored)));
				trajectory.Points[6+i].SetRotation(GetRootRotation(reference, mirrored));
				trajectory.Points[6+i].SetVelocity(GetRootVelocity(reference, mirrored));
				trajectory.Points[6+i].SetSpeed(GetSpeed(reference, mirrored));
				trajectory.Points[6+i].Styles = styleModule == null ? new float[0] : styleModule.GetStyle(reference);
				trajectory.Points[6+i].Phase = phaseModule == null ? 0f : Mathf.Repeat(phaseModule.GetPhase(Data.GetLastFrame(), mirrored) + Utility.PhaseUpdate(phaseModule.GetPhase(reference, mirrored), phaseModule.GetPhase(Data.GetLastFrame(), mirrored)), 1f);
				trajectory.Points[6+i].Gaits = gaitModule == null ? new float[0] : gaitModule.GetGait(reference);
			} else {
				Frame future = Data.GetFrame(Mathf.Clamp(frame.Timestamp + delta, 0f, Data.GetTotalTime()));
				trajectory.Points[6+i].SetTransformation(GetRootTransformation(future, mirrored));
				trajectory.Points[6+i].SetVelocity(GetRootVelocity(future, mirrored));
				trajectory.Points[6+i].SetSpeed(GetSpeed(future, mirrored));
				trajectory.Points[6+i].Styles = styleModule == null ? new float[0] : styleModule.GetStyle(future);
				trajectory.Points[6+i].Phase = phaseModule == null ? 0f : phaseModule.GetPhase(future, mirrored);
				trajectory.Points[6+i].Gaits = gaitModule == null ? new float[0] : gaitModule.GetGait(future);
			}
		}


		//Signals
		// we don't currently use signals so can ignore this.
		// for(int i=0; i<7; i++) {
		// 	float delta = -1f + (float)i/6f;
		// 	if(frame.Timestamp + delta < 0f) {
		// 		float pivot = - frame.Timestamp - delta;
		// 		float clamped = Mathf.Clamp(pivot, 0f, Data.GetTotalTime());
		// 		Frame reference = Data.GetFrame(clamped);
		// 		trajectory.Points[i].Signals = styleModule == null ? new float[0] : styleModule.GetInverseSignal(reference); 
		// 	} else {
		// 		Frame pivot = Data.GetFrame(Mathf.Clamp(frame.Timestamp + delta, 0f, Data.GetTotalTime()));
		// 		trajectory.Points[i].Signals = styleModule == null ? new float[0] : styleModule.GetSignal(pivot);
		// 	}
		// }
		// for(int i=7; i<12; i++) {
		// 	trajectory.Points[i].Signals = (float[])trajectory.Points[6].Signals.Clone();
		// }

		// //Finish
		// for(int i=0; i<trajectory.Points.Last().Signals.Length; i++) {
		// 	for(int j=7; j<12; j++) {
		// 		if(trajectory.Points.Last().Signals[i] > 0f && trajectory.Points.Last().Signals[i] < 1f) {
		// 			int pivot = j-1;
		// 			trajectory.Points[j].Styles[i] = Mathf.Max(trajectory.Points[pivot].Styles[i], trajectory.Points[j].Styles[i]);
		// 		}
		// 		if(trajectory.Points.Last().Signals[i] < 0f && trajectory.Points.Last().Signals[i] > -1f) {
		// 			int pivot = j-1;
		// 			trajectory.Points[j].Styles[i] = Mathf.Min(trajectory.Points[pivot].Styles[i], trajectory.Points[j].Styles[i]);
		// 		}
		// 		if(trajectory.Points.Last().Signals[i] == 0f) {
		// 			trajectory.Points[j].Styles[i] = trajectory.Points[6].Styles[i];
		// 		}
		// 		if(trajectory.Points.Last().Signals[i] == 1f || trajectory.Points.Last().Signals[i] == -1f) {
		// 			trajectory.Points[j].Styles[i] = trajectory.Points[6].Styles[i];
		// 		}
		// 	}
		// }
		// if(trajectory.Points.Last().Signals.AbsSum() == 0f) {
		// 	for(int j=7; j<12; j++) {
		// 		trajectory.Points[j].Styles = (float[])trajectory.Points[6].Styles.Clone();
		// 	}
		// }
		
		return trajectory;
	}

	protected override void DerivedDraw(MotionEditor editor) {
		UltiDraw.Begin();
		if(ShowPath) {
			Frame[] frames = editor.GetCurrentFile().Data.Frames;
			int step = (int)editor.GetCurrentFile().Data.Framerate/10;
			for(int i=0; i<frames.Length; i+=step) {
				UltiDraw.DrawCircle(GetRootPosition(frames[i], editor.Mirror), 0.025f, UltiDraw.Black);
			}
		}
		UltiDraw.End();
		GetTrajectory(editor.GetCurrentFrame(), editor.Mirror).Draw();
	}

	protected override void DerivedInspector(MotionEditor editor) {
		ShowPath = EditorGUILayout.Toggle("Show Path", ShowPath);
		ForwardAxis = (MotionData.AXIS)EditorGUILayout.EnumPopup("Forward Axis", ForwardAxis);
		Ground = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(EditorGUILayout.MaskField("Ground Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(Ground), InternalEditorUtility.layers));
	}

	public Matrix4x4 GetRootTransformation(Frame frame, bool mirrored) {
		return Matrix4x4.TRS(GetRootPosition(frame, mirrored), GetRootRotation(frame, mirrored), Vector3.one);
	}

	public Vector3 GetRootPosition(Frame frame, bool mirrored) {
		return Utility.ProjectGround(frame.GetBoneTransformation(0, mirrored).GetPosition(), Ground);
	}

	public Quaternion GetRootRotation(Frame frame, bool mirrored) {
		
		//Vector3 v1 = GetBoneTransformation(Data.Source.FindBone("RightHip").Index, mirrored, Data.RootSmoothing).GetPosition() - GetBoneTransformation(Data.Source.FindBone("LeftHip").Index, mirrored, Data.RootSmoothing).GetPosition();
		//Vector3 v2 = GetBoneTransformation(Data.Source.FindBone("RightShoulder").Index, mirrored, Data.RootSmoothing).GetPosition() - GetBoneTransformation(Data.Source.FindBone("LeftShoulder").Index, mirrored, Data.RootSmoothing).GetPosition();
		//v1.y = 0f;
		//v2.y = 0f;
		//Vector3 v = (v1+v2).normalized;
		//Vector3 forward = -Vector3.Cross(v, Vector3.up);
		//forward.y = 0f;
		
		/*
		Vector3 neck = GetBoneTransformation(Data.Source.FindBone("Neck").Index, mirrored, Data.RootSmoothing).GetPosition();
		Vector3 hips = GetBoneTransformation(Data.Source.FindBone("Hips").Index, mirrored, Data.RootSmoothing).GetPosition();
		//int leftShoulder = Data.Source.FindBone("LeftShoulder").Index;
		//int rightShoulder = Data.Source.FindBone("RightShoulder").Index;
		//int leftUpLeg = Data.Source.FindBone("LeftUpLeg").Index;
		////int rightUpLeg = Data.Source.FindBone("RightUpLeg").Index;
		Vector3 forward = Vector3.zero;
		forward += neck - hips;
		//forward += GetBoneTransformation(leftShoulder, mirrored, Data.RootSmoothing).GetPosition() - GetBoneTransformation(leftUpLeg, mirrored, Data.RootSmoothing).GetPosition();
		//forward += GetBoneTransformation(rightShoulder, mirrored, Data.RootSmoothing).GetPosition() - GetBoneTransformation(rightUpLeg, mirrored, Data.RootSmoothing).GetPosition();
		*/

		//Vector3 forward = GetBoneTransformation(Data.Source.FindBoneContains("Hip").Index, mirrored, Data.RootSmoothing).GetForward();

		Vector3 forward = frame.GetBoneTransformation(0, mirrored).GetForward();
		forward = Quaternion.FromToRotation(Vector3.forward, Data.GetAxis(ForwardAxis)) * forward;
		forward.y = 0f;
		return Quaternion.LookRotation(forward.normalized, Vector3.up);
	}

	public Vector3 GetRootVelocity(Frame frame, bool mirrored) {
		if(frame.Index == 1) {
			return GetRootVelocity(frame.GetNextFrame(), mirrored);
		} else {
			Vector3 velocity = (frame.GetBoneTransformation(0, mirrored).GetPosition() - frame.GetPreviousFrame().GetBoneTransformation(0, mirrored).GetPosition()) * Data.Framerate;
			velocity.y = 0f;
			return velocity;
		}
	}

	public float GetSpeed(Frame frame, bool mirrored) {
		float length = 0f;
		Vector3[] positions = new Vector3[6];
		positions[0] = GetRootPosition(frame, mirrored);
		positions[0].y = 0f;
		for(int i=1; i<=5; i++) {
			Frame future = Data.GetFrame(Mathf.Clamp(frame.Timestamp + (float)i/5f, 0f, Data.GetTotalTime()));
			positions[i] = GetRootPosition(future, mirrored);
			positions[i].y = 0f;
		}
		for(int i=1; i<=5; i++) {
			length += Vector3.Distance(positions[i-1], positions[i]);
		}
		return length;
	}

}
#endif

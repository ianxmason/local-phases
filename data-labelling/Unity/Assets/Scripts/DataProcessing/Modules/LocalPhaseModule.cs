#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEditor;
using DeepLearning;


// Foot phases are determined based on contacts (except in the hopping case - not sure what do here)

// The phase is scaled (either by the velocity in the character coordinate space or thresholded by the variance along the first principal component)
// this happens so that bones that are still have no phase/very small phase (e.g. raised right arm)

// Some effort has been put into trying to make the autolabelling more robust

public class LocalPhaseModule : Module {

    // TODO (opt): go through file cleaning and solving all TODO (opt)s

    public enum SOURCE {PCALocalPositionsSigned, PCALocalPositionsProjected, LocalPositions, Velocities, LocalVelocities, Hybrid};
    public SOURCE Source = SOURCE.Hybrid;

    public bool ShowNormalized = true;
    public bool ShowHighlighted = true;
    public bool ShowSource = true;
    public bool ShowValues = false;
    public bool ShowFitting = true;
    public bool ShowZero = true;
    public bool ShowPhase = true;
    public bool ShowWindow = true;
    // public bool DisplayValues = true;

    public int MaxIterations = 10;
    public int Individuals = 50;
    public int Elites = 5;
    public float MaxFrequency = 4f;
    public float Exploration = 0.2f;
    public float Memetism = 0.1f;  // TODO: memetism doesn't do anything once Accord is removed

    public bool ApplyNormalization = true; //
    public bool ApplyButterworth = true;
    
    public bool LocalVelocities = true;

    [NonSerialized] private bool ShowParameters = false;

    public Function[] Functions = new Function[0];

    private string[] Identifiers = null;

    private bool Token = false;
    private bool[] Threads = null;
    private int[] Iterations = null;
    private float[] Progress = null;



	// public float MaximumVelocity = 10f;
	// public float VelocityThreshold = -100f; // 0.1f;

	// public PhaseFunction RegularPhaseFunction = null;
	// public PhaseFunction InversePhaseFunction = null;
	public bool[] Variables = new bool[0];

	// public bool ShowVelocities = true;
    // public bool ShowFilteredVelocities = true;
	// public bool ShowCycle = true;
	// public bool ShowGradients = false;

	// private bool Optimising = false;	
	

	public override TYPE Type() {
		return TYPE.LocalPhase;
	}

	public override Module Initialise(MotionData data) {
		Data = data;
		Inspect = true;
		Visualise = false;
		// RegularPhaseFunction = new PhaseFunction(this);
		// InversePhaseFunction = new PhaseFunction(this);
		Variables = new bool[Data.Source.Bones.Length];
        // Default hand and feet are set as phase detector variables
        ToggleVariable(17);
        ToggleVariable(26); //
		return this;
	}

	// public void SetMaximumVelocity(float value) {
	// 	value = Mathf.Max(1f, value);
	// 	if(MaximumVelocity != value) {
	// 		MaximumVelocity = value;
	// 		RegularPhaseFunction.ComputeVelocities();
	// 		InversePhaseFunction.ComputeVelocities();
    //         RegularPhaseFunction.FilterVelocities();
    //         InversePhaseFunction.FilterVelocities();
	// 		RegularPhaseFunction.FiniteDifferences();
    //         InversePhaseFunction.FiniteDifferences();
	// 	}
	// }

	// public void SetVelocityThreshold(float value) {
	// 	// value = Mathf.Max(0f, value);
	// 	value = Mathf.Max(-200f, value);
	// 	if(VelocityThreshold != value) {
	// 		VelocityThreshold = value;
	// 		RegularPhaseFunction.ComputeVelocities();
	// 		InversePhaseFunction.ComputeVelocities();
    //         RegularPhaseFunction.FilterVelocities();
    //         InversePhaseFunction.FilterVelocities();
	// 		RegularPhaseFunction.FiniteDifferences();
    //         InversePhaseFunction.FiniteDifferences();
	// 	}
	// }

	public void ToggleVariable(int index) {
		Variables[index] = !Variables[index];
	}

	// public float GetPhase(Frame frame, bool mirrored) {
	// 	return mirrored ? InversePhaseFunction.GetPhase(frame) : RegularPhaseFunction.GetPhase(frame);
	// }

	protected override void DerivedDraw(MotionEditor editor) {
        // TODO (opt): write this drawing to work with mirroring as well. // Easy way is to swap functionIdx of regular and inverse. Other way is use GetSymmetricFunction()
        // TODO (opt): write the drawing function for different SOURCE options - currently only use projectedDistances, what about signedDistances, velocities etc.
        // We use only projectedDistances so we don't need these other drawing options right now
		// Draws a plane using the two biggest principal components
        int functionIdx = 0;
        for(int i=0; i<Data.Source.Bones.Length; i++) {
			if(Variables[i]) {
                Quaternion RegularQuat = Quaternion.LookRotation(Functions[functionIdx].GetPcs()[1], Functions[functionIdx].GetPcs()[2]);
                RegularQuat = RegularQuat.GetRelativeRotationFrom(editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror));
                Vector3 RegularMeanPos = Functions[functionIdx].GetPositionsMean().GetRelativePositionFrom(editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror));
		        float RegularProjectedDistance  = Functions[functionIdx].GetProjectedDistances()[editor.GetCurrentFrame().Index];

                UltiDraw.Begin();

                UltiDraw.DrawCuboid(RegularMeanPos, RegularQuat, new Vector3(0.01f, 1f, 1f), UltiDraw.Red);
                UltiDraw.DrawArrow(RegularMeanPos, RegularMeanPos + editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror).rotation * (0.4f * Functions[functionIdx].GetPcs()[0]),
                                                    0.9f, 0.01f, 0.1f, UltiDraw.Magenta);

                // Debug.Log(Functions[functionIdx].Bone);
                // Debug.Log(Functions[functionIdx].GetPcs()[0]);
                // // Debug.Log(Functions[functionIdx].GetPcs()[1]);
                // // Debug.Log(Functions[functionIdx].GetPcs()[2]);
                // Debug.Log("---");
                
                // TODO (opt): try spheres as well as cuboids
                // if(RegularProjectedDistance == 0){
                //     UltiDraw.DrawCuboid(editor.GetCurrentFrame().GetBoneTransformation(Functions[functionIdx].Bone, editor.Mirror).GetPosition(), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f), UltiDraw.Red);
                // }
                // else {
                //     UltiDraw.DrawCuboid(editor.GetCurrentFrame().GetBoneTransformation(Functions[functionIdx].Bone, editor.Mirror).GetPosition(), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f), (RegularProjectedDistance > 0 ? UltiDraw.Green : UltiDraw.Black));
                // }

                UltiDraw.End();

                functionIdx += 1;

                Quaternion InverseQuat = Quaternion.LookRotation(Functions[functionIdx].GetPcs()[1], Functions[functionIdx].GetPcs()[2]);
		        InverseQuat = InverseQuat.GetRelativeRotationFrom(editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror));
                Vector3 InverseMeanPos = Functions[functionIdx].GetPositionsMean().GetRelativePositionFrom(editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror));
                float InverseProjectedDistance  = Functions[functionIdx].GetProjectedDistances()[editor.GetCurrentFrame().Index];

                UltiDraw.Begin();

                UltiDraw.DrawCuboid(InverseMeanPos, InverseQuat, new Vector3(0.01f, 1f, 1f), UltiDraw.Blue);
                UltiDraw.DrawArrow(InverseMeanPos, InverseMeanPos + editor.GetCurrentFrame().GetBoneTransformation(0, editor.Mirror).rotation * (0.4f * Functions[functionIdx].GetPcs()[0]),
                                                     0.9f, 0.01f, 0.1f, UltiDraw.Magenta);

                // Debug.Log(Functions[functionIdx].Bone);
                // Debug.Log(Functions[functionIdx].GetPcs()[0]);
                // // Debug.Log(Functions[functionIdx].GetPcs()[1]);
                // // Debug.Log(Functions[functionIdx].GetPcs()[2]);
                // Debug.Log("###");

                // TODO (opt): try spheres as well as cuboids
                // if(InverseProjectedDistance == 0){
                //     UltiDraw.DrawCuboid(editor.GetCurrentFrame().GetBoneTransformation(Functions[functionIdx].Bone, editor.Mirror).GetPosition(), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f), UltiDraw.Red);
                // }
                // else {
                //     UltiDraw.DrawCuboid(editor.GetCurrentFrame().GetBoneTransformation(Functions[functionIdx].Bone, editor.Mirror).GetPosition(), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f), (InverseProjectedDistance > 0 ? UltiDraw.Green : UltiDraw.Black));
                // }

                UltiDraw.End();

                 functionIdx += 1;
			}
        }


		/*
		UltiDraw.Begin();
		for(int i=0; i<Variables.Length; i++) {
			if(Variables[i]) {
				UltiDraw.DrawSphere(editor.GetCurrentFrame().GetBoneTransformation(i, editor.Mirror).GetPosition(), Quaternion.identity, 0.05f, UltiDraw.Red);
			}
		}
		UltiDraw.End();
		*/

        // TODO (opt): add in the showparameters drawing from SebastiansAlignmentModule and integrate into this file
	}

	protected override void DerivedInspector(MotionEditor editor) {
        Validate();
        Frame frame = editor.GetCurrentFrame();
        ShowNormalized = EditorGUILayout.Toggle("Show Normalized", ShowNormalized);
        ShowHighlighted = EditorGUILayout.Toggle("Show Highlighted", ShowHighlighted);
        ShowSource = EditorGUILayout.Toggle("Show Source", ShowSource);
        ShowValues = EditorGUILayout.Toggle("Show Values", ShowValues);
        ShowFitting = EditorGUILayout.Toggle("Show Fitting", ShowFitting);
        ShowZero = EditorGUILayout.Toggle("Show Zero", ShowZero);
        ShowPhase = EditorGUILayout.Toggle("Show Phase", ShowPhase);
        ShowWindow = EditorGUILayout.Toggle("Show Window", ShowWindow);
        // DisplayValues = EditorGUILayout.Toggle("Display Values", DisplayValues);

        MaxIterations = EditorGUILayout.IntField("Max Iterations", MaxIterations);
        Individuals = EditorGUILayout.IntField("Individuals", Individuals);
        Elites = EditorGUILayout.IntField("Elites", Elites);
        MaxFrequency = EditorGUILayout.FloatField("Max Frequency", MaxFrequency);
        Exploration = EditorGUILayout.Slider("Exploration", Exploration, 0f, 1f);
        Memetism = EditorGUILayout.Slider("Memetism", Memetism, 0f, 1f);

        ApplyNormalization = EditorGUILayout.Toggle("Apply Normalization", ApplyNormalization);
        ApplyButterworth = EditorGUILayout.Toggle("Apply Butterworth", ApplyButterworth);
        LocalVelocities = EditorGUILayout.Toggle("Local Velocities", LocalVelocities);

        ShowParameters = EditorGUILayout.Toggle("Show Parameters", ShowParameters);

        Source = (SOURCE)EditorGUILayout.EnumPopup("Source", Source);

        int index = EditorGUILayout.Popup("Phase Detector", 0, ArrayExtensions.Concat("Select...", Data.Source.GetNames()));
		if(index > 0) {
			ToggleVariable(index-1);
		}
		for(int i=0; i<Data.Source.Bones.Length; i++) {
			if(Variables[i]) {
				using(new EditorGUILayout.VerticalScope ("Box")) {
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField(Data.Source.Bones[i].Name);
					EditorGUILayout.LabelField(Data.Source.Bones[Data.Symmetry[i]].Name);
					EditorGUILayout.EndHorizontal();
				}
			}
		}

        if(Utility.GUIButton("Compute", UltiDraw.DarkGrey, UltiDraw.White)) {
            foreach(Function f in Functions) {
                f.Compute(true);
            }
        }

        bool fitting = IsFitting();
        EditorGUI.BeginDisabledGroup(Token);
        if(Utility.GUIButton(fitting ? "Stop" : "Optimize", fitting ? UltiDraw.DarkRed : UltiDraw.DarkGrey, UltiDraw.White)) {
            if(!fitting) {
                StartFitting();
            } else {
                StopFitting();
            }
        }
        EditorGUI.EndDisabledGroup();
        
        float max = 0f;
        if(ShowHighlighted) {
            foreach(Function f in Functions) {
                max = Mathf.Max(max, (float)f.Amplitudes.Max());
            }
        }

        float height = 50f;

        for(int i=0; i<Functions.Length; i++) {
            
            // This is strange as doesn't swap right and left toe values, only swaps their order in the editor
            // Function f = editor.Mirror ? Functions[i].GetSymmetricFunction() : Functions[i];  
            // All that really matters is when we call GetPhase in the exporter that the order of the phases is swapped, so it is still okay for exporting
            Function f = Functions[i];
            EditorGUILayout.BeginHorizontal();

            Utility.GUIButton(f.GetName(), UltiDraw.GetRainbowColor(i, Functions.Length).Transparent(0.5f), UltiDraw.Black, 150f, height);

            EditorGUILayout.BeginVertical(GUILayout.Height(height));
            Rect ctrl = EditorGUILayout.GetControlRect();
            Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
            EditorGUI.DrawRect(rect, UltiDraw.Black);

            UltiDraw.Begin();

            float startTime = frame.Timestamp-editor.GetWindow()/2f;
            float endTime = frame.Timestamp+editor.GetWindow()/2f;
            if(startTime < 0f) {
                endTime -= startTime;
                startTime = 0f;
            }
            if(endTime > Data.GetTotalTime()) {
                startTime -= endTime-Data.GetTotalTime();
                endTime = Data.GetTotalTime();
            }
            startTime = Mathf.Max(0f, startTime);
            endTime = Mathf.Min(Data.GetTotalTime(), endTime);
            int start = Data.GetFrame(startTime).Index;
            int end = Data.GetFrame(endTime).Index;
            int elements = end-start;

            Vector3 prevPos = Vector3.zero;
            Vector3 newPos = Vector3.zero;
            Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
            Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

            //Zero
            if(ShowZero) {
                prevPos.x = rect.xMin;
                prevPos.y = rect.yMax - (float)(ShowNormalized ? 0.0.Normalize(f.MinValue, f.MaxValue, 0f, 1f) : 0f) * rect.height;
                newPos.x = rect.xMin + rect.width;
                newPos.y = rect.yMax - (float)(ShowNormalized ? 0.0.Normalize(f.MinValue, f.MaxValue, 0f, 1f) : 0f) * rect.height;
                UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Magenta.Transparent(0.5f));
            }

            //Source
            if(ShowSource) {
                for(int j=1; j<elements; j++) {
                    prevPos.x = rect.xMin + (float)(j-1)/(elements-1) * rect.width;
                    prevPos.y = rect.yMax - (float)f.GetSource(start+j-1-1, ShowNormalized) * rect.height;
                    newPos.x = rect.xMin + (float)(j)/(elements-1) * rect.width;
                    newPos.y = rect.yMax - (float)f.GetSource(start+j-1, ShowNormalized) * rect.height;
                    UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White);
                }
            }

            //Values
            if(ShowValues) {
                for(int j=1; j<elements; j++) {
                    prevPos.x = rect.xMin + (float)(j-1)/(elements-1) * rect.width;
                    prevPos.y = rect.yMax - (float)f.GetValue(start+j-1-1, ShowNormalized) * rect.height;
                    newPos.x = rect.xMin + (float)(j)/(elements-1) * rect.width;
                    newPos.y = rect.yMax - (float)f.GetValue(start+j-1, ShowNormalized) * rect.height;
                    UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White);
                }
            }

            //Fitting
            if(ShowFitting) {
                for(int j=1; j<elements; j++) {
                    prevPos.x = rect.xMin + (float)(j-1)/(elements-1) * rect.width;
                    prevPos.y = rect.yMax - (float)f.GetFit(start+j-1-1, ShowNormalized) * rect.height;
                    newPos.x = rect.xMin + (float)(j)/(elements-1) * rect.width;
                    newPos.y = rect.yMax - (float)f.GetFit(start+j-1, ShowNormalized) * rect.height;
                    UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Green);
                }
            }

            //Phase
            if(ShowPhase) {
                for(int j=0; j<elements; j++) {
                    prevPos.x = rect.xMin + (float)(j)/(elements-1) * rect.width;
                    prevPos.y = rect.yMax;
                    newPos.x = rect.xMin + (float)(j)/(elements-1) * rect.width;
                    newPos.y = rect.yMax - (float)f.GetPhase(start+j-1) * rect.height;
                    float weight = ShowHighlighted ? (float)f.GetAmplitude(start+j-1).Normalize(0f, max, 0f, 1f) : 1f;
                    // weight = 0.5f;  // Temp hardcoding for visualisation
                    UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Cyan.Transparent(weight));
                }
            }

            //Current Pivot (and +/- one second very faintly)
            float pStart = (float)(Data.GetFrame(Mathf.Clamp(frame.Timestamp-1f, 0f, Data.GetTotalTime())).Index-start) / (float)elements;
            float pEnd = (float)(Data.GetFrame(Mathf.Clamp(frame.Timestamp+1f, 0f, Data.GetTotalTime())).Index-start) / (float)elements;
            float pLeft = rect.x + pStart * rect.width;
            float pRight = rect.x + pEnd * rect.width;
            Vector3 pA = new Vector3(pLeft, rect.y, 0f);
            Vector3 pB = new Vector3(pRight, rect.y, 0f);
            Vector3 pC = new Vector3(pLeft, rect.y+rect.height, 0f);
            Vector3 pD = new Vector3(pRight, rect.y+rect.height, 0f);
            UltiDraw.DrawTriangle(pA, pC, pB, UltiDraw.White.Transparent(0.1f));
            UltiDraw.DrawTriangle(pB, pC, pD, UltiDraw.White.Transparent(0.1f));
            top.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
            bottom.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
            UltiDraw.DrawLine(top, bottom, UltiDraw.Yellow);

            // //Phase Window
            // if(ShowWindow) {
            //    int padding = f.GetPhaseWindow(frame.Index-1) / 2;
            //    container.Editor.DrawRect(Data.GetFrame(frame.Index - padding), Data.GetFrame(frame.Index + padding), 1f, UltiDraw.Gold.Transparent(0.25f), rect);
            // }

            // //Window
            if(ShowWindow) {
                int padding = f.Windows[frame.Index-1] / 2;
                float wStart = (float)(frame.Index - padding-start) / (float)elements;
                float wEnd = (float)(frame.Index + padding-start) / (float)elements;
                float wLeft = rect.x + wStart * rect.width;
                float wRight = rect.x + wEnd * rect.width;
                Vector3 wA = new Vector3(wLeft, rect.y, 0f);
                Vector3 wB = new Vector3(wRight, rect.y, 0f);
                Vector3 wC = new Vector3(wLeft, rect.y+rect.height, 0f);
                Vector3 wD = new Vector3(wRight, rect.y+rect.height, 0f);
                UltiDraw.DrawTriangle(wA, wC, wB, UltiDraw.IndianRed.Transparent(0.5f));
                UltiDraw.DrawTriangle(wB, wC, wD, UltiDraw.IndianRed.Transparent(0.5f));
            }

            UltiDraw.End();

            // container.Editor.DrawPivot(rect);
            
            EditorGUILayout.EndVertical();

            //Progress Bar
            if(fitting && Threads != null && Iterations != null && Progress != null && Threads.Length > 0 && Iterations.Length > 0 && Progress.Length > 0) {
                float ratio = (float)Iterations[i] / (float)MaxIterations;
                // EditorGUILayout.LabelField(Mathf.RoundToInt(100f * ratio) + "%", GUILayout.Width(40f));

                EditorGUI.DrawRect(new Rect(ctrl.x, ctrl.y, ratio * ctrl.width, height), UltiDraw.Lerp(UltiDraw.Red, UltiDraw.Green, ratio).Transparent(0.5f));
                
                // if(Progress[i] > 0f && Progress[i] < 1f) {
                //     EditorGUI.DrawRect(new Rect(ctrl.x, ctrl.y, Progress[i] * ctrl.width, height), UltiDraw.Lerp(UltiDraw.Red, UltiDraw.Green, Progress[i]).Opacity(0.5f));
                // }
            }

            EditorGUILayout.EndHorizontal();

            // if(DisplayValues) {
            //     EditorGUILayout.BeginHorizontal();
            //     EditorGUI.BeginDisabledGroup(true);

            //     // float value = (float)Functions[i].GetValue(editor.GetCurrentFrame().Index-1, false);
            //     // value = Mathf.Round(value * 100f) / 100f;
            //     // EditorGUILayout.FloatField(value, GUILayout.Width(35f));

            //     GUILayout.FlexibleSpace();

            //     float amplitude = (float)GetAmplitude(i, container.Editor.GetTimestamp(), false);
            //     amplitude = Mathf.Round(amplitude * 100f) / 100f;
            //     EditorGUILayout.LabelField("Amplitude", GUILayout.Width(100f));
            //     EditorGUILayout.FloatField(amplitude, GUILayout.Width(50f));

            //     float phase = (float)GetPhase(i, container.Editor.GetTimestamp(), false);
            //     phase = Mathf.Round(phase * 100f) / 100f;
            //     EditorGUILayout.LabelField("Phase", GUILayout.Width(100f));
            //     EditorGUILayout.FloatField(phase, GUILayout.Width(50f));
                
            //     GUILayout.FlexibleSpace();

            //     EditorGUI.EndDisabledGroup();
            //     EditorGUILayout.EndHorizontal();
            // }
        }
        // {
            // EditorGUILayout.BeginHorizontal();

            // Utility.GUIButton("Amplitudes", UltiDraw.White, UltiDraw.Black, 150f, height);

            // EditorGUILayout.BeginVertical(GUILayout.Height(height));
            // Rect ctrl = EditorGUILayout.GetControlRect();
            // Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, height);
            // EditorGUI.DrawRect(rect, UltiDraw.Black);

            // UltiDraw.Begin();

            // Vector3 prevPos = Vector3.zero;
            // Vector3 newPos = Vector3.zero;
            // Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
            // Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

            // for(int i=0; i<Functions.Length; i++) {
            //     Function f = Functions[i];
            //     for(int j=1; j<view.z; j++) {
            //         prevPos.x = rect.xMin + (float)(j-1)/(view.z-1) * rect.width;
            //         prevPos.y = rect.yMax - (float)f.GetAmplitude(view.x+j-1-1).Normalize(0f, max, 0f, 1f) * rect.height;
            //         newPos.x = rect.xMin + (float)(j)/(view.z-1) * rect.width;
            //         newPos.y = rect.yMax - (float)f.GetAmplitude(view.x+j-1).Normalize(0f, max, 0f, 1f) * rect.height;
            //         UltiDraw.DrawLine(prevPos, newPos, UltiDraw.GetRainbowColor(i, Functions.Length));
            //     }
            // }

            // UltiDraw.End();

            // container.Editor.DrawPivot(rect);
            
            // EditorGUILayout.EndVertical();

            // EditorGUILayout.EndHorizontal();
        // }
        EditorGUILayout.HelpBox("Active Threads: " + (Threads == null ? 0 : Threads.Count(true)), MessageType.None);
	}

    public string[] GetIdentifiers() {
        if(!Identifiers.Verify(Functions.Length)) {
            Identifiers = new string[Functions.Length];
            for(int i=0; i<Functions.Length; i++) {
                Identifiers[i] = Functions[i].GetName();
            }
        }
        return Identifiers;
    }

    public float[] GetPhases(float timestamp, bool mirrored) {
        float[] values = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            values[i] = GetPhase(i, timestamp, mirrored);
        }
        return values;
    }

    public float GetPhase(int function, float timestamp, bool mirrored) {
		float start = Data.GetFirstFrame().Timestamp;
		float end = Data.GetLastFrame().Timestamp;
		if(timestamp < start || timestamp > end) {
            float boundary = Mathf.Clamp(timestamp, start, end);
            float pivot = 2f*boundary - timestamp;
            float repeated = Mathf.Repeat(pivot-start, end-start) + start;
            return
            Mathf.Repeat(
                (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetPhase(Data.GetFrame(boundary).Index-1) -
                Utility.SignedPhaseUpdate(
                    (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetPhase(Data.GetFrame(boundary).Index-1),
                    (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetPhase(Data.GetFrame(repeated).Index-1)
                ), 1f
            );
        } else {
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetPhase(Data.GetFrame(timestamp).Index-1);
        }
    }

    public float[] GetAmplitudes(float timestamp, bool mirrored) {
        float[] values = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            values[i] = GetAmplitude(i, timestamp, mirrored);
        }
        return values;
    }

    public float GetAmplitude(int function, float timestamp, bool mirrored) {
		float start = Data.GetFirstFrame().Timestamp;
		float end = Data.GetLastFrame().Timestamp;
		if(timestamp < start || timestamp > end) {
            float boundary = Mathf.Clamp(timestamp, start, end);
            float pivot = 2f*boundary - timestamp;
            float repeated = Mathf.Repeat(pivot-start, end-start) + start;
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetAmplitude(Data.GetFrame(repeated).Index-1);
        } else {
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetAmplitude(Data.GetFrame(timestamp).Index-1);
        }
    }

    public float[] GetFrequencies(float timestamp, bool mirrored) {
        float[] values = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            values[i] = GetFrequency(i, timestamp, mirrored);
        }
        return values;
    }

    public float GetFrequency(int function, float timestamp, bool mirrored) {
		float start =  Data.GetFirstFrame().Timestamp;
		float end = Data.GetLastFrame().Timestamp;
		if(timestamp < start || timestamp > end) {
            float boundary = Mathf.Clamp(timestamp, start, end);
            float pivot = 2f*boundary - timestamp;
            float repeated = Mathf.Repeat(pivot-start, end-start) + start;
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetFrequency(Data.GetFrame(repeated).Index-1);
        } else {
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetFrequency(Data.GetFrame(timestamp).Index-1);
        }
    }

    public float[] GetShifts(float timestamp, bool mirrored) {
        float[] values = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            values[i] = GetShift(i, timestamp, mirrored);
        }
        return values;
    }

    public float GetShift(int function, float timestamp, bool mirrored) {
		float start =  Data.GetFirstFrame().Timestamp;
		float end = Data.GetLastFrame().Timestamp;
		if(timestamp < start || timestamp > end) {
            float boundary = Mathf.Clamp(timestamp, start, end);
            float pivot = 2f*boundary - timestamp;
            float repeated = Mathf.Repeat(pivot-start, end-start) + start;
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetShift(Data.GetFrame(repeated).Index-1);
        } else {
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetShift(Data.GetFrame(timestamp).Index-1);
        }
    }

    public float[] GetOffsets(float timestamp, bool mirrored) {
        float[] values = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            values[i] = GetOffset(i, timestamp, mirrored);
        }
        return values;
    }

    public float GetOffset(int function, float timestamp, bool mirrored) {
		float start =  Data.GetFirstFrame().Timestamp;
		float end = Data.GetLastFrame().Timestamp;
		if(timestamp < start || timestamp > end) {
            float boundary = Mathf.Clamp(timestamp, start, end);
            float pivot = 2f*boundary - timestamp;
            float repeated = Mathf.Repeat(pivot-start, end-start) + start;
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetOffset(Data.GetFrame(repeated).Index-1);
        } else {
            return (float)(mirrored ? Functions[function].GetSymmetricFunction() : Functions[function]).GetOffset(Data.GetFrame(timestamp).Index-1);
        }
    }

    // TODO (opt): is this needed? if so - what is Asset.GetDeltaTime()
    // public float[] GetUpdateRates(float from, float to, bool mirrored) {
    //     float[] rates = new float[Functions.Length];
    //     for(int i=0; i<Functions.Length; i++) {
    //         float delta = 0f;
    //         for(float t=from; t<to-Asset.GetDeltaTime(); t+=Asset.GetDeltaTime()) {
    //             delta += Utility.SignedPhaseUpdate(GetPhase(i, t, mirrored), GetPhase(i, t+Asset.GetDeltaTime(), mirrored));
    //         }
    //         rates[i] = delta / (to-from);
    //     }
    //     return rates;
    // }

    public void StartFitting() {
        //Create Functions - one Function for every bone
        // Functions = new Function[Data.Source.Bones.Length];
        // for(int i=0; i<Functions.Length; i++) {
        //     Functions[i] = new Function(this, Data.Source.Bones[i].Index);
        // }

        // Create Functions - only the Functions required
        int functionCount = 0;
        for(int i=0; i<Data.Source.Bones.Length; i++) {
			if(Variables[i]) {
				functionCount += 2;
			}
        }
        Functions = new Function[functionCount];
        int functionIdx = 0;
        for(int i=0; i<Data.Source.Bones.Length; i++) {
			if(Variables[i]) {
				 Functions[functionIdx] = new Function(this, Data.Source.Bones[i].Index);
                 functionIdx += 1;
                 Functions[functionIdx] = new Function(this, Data.Source.Bones[Data.Symmetry[i]].Index);
                 functionIdx += 1;
			}
        }
        
        foreach(Function f in Functions) {
            f.Preprocess();
        }
        
        Token = false;
        Threads = new bool[Functions.Length];
        Iterations = new int[Functions.Length];
        Progress = new float[Functions.Length];
        for(int i=0; i<Functions.Length; i++) {
            int thread = i;
            Threads[thread] = true;
            Task.Factory.StartNew(() => {
                Functions[thread].Optimize(ref Token, ref Threads, ref Iterations, ref Progress, thread);
            });
            // Functions[thread].Optimize(ref Token, ref Threads, ref Iterations, ref Progress, thread);
        }

        // Task.Factory.StartNew(() => {
        //     for(int i=0; i<Functions.Length; i++) {
        //         int thread = i;
        //         Threads[thread] = true;
        //         // Task.Factory.StartNew(() => {
        //         //     Functions[thread].Optimize(ref Token, ref Threads, ref Iterations, ref Progress, thread);
        //         // });
        //         Functions[thread].Optimize(ref Token, ref Threads, ref Iterations, ref Progress, thread);
        //     }                
        // });
            
    }

    public void StopFitting() {
        Token = true;
        Task.Factory.StartNew(() => {
            while(IsFitting()) {
                System.Threading.Thread.Sleep(1);
                // Debug.Log("Inside thread");
                // Debug.Log(Token);
            }
            // Debug.Log("Ouside while loop");
            // Debug.Log(Token);
            Token = false;
        });
        // Debug.Log("Outside thread");
        // Debug.Log(Token);
    }

    public bool IsFitting() {
        if(Threads != null && Threads.Any(true)) {
            return true;
        } else {
            Token = false;
            Threads = null;
            Iterations = null;
            return false;
        }
    }


    private void Validate() {
        foreach(Function f in Functions) {
            f.Values = f.Values.Validate(Data.Frames.Length);
            f.Fit = f.Fit.Validate(Data.Frames.Length);
            f.Phases = f.Phases.Validate(Data.Frames.Length);
            f.Amplitudes = f.Amplitudes.Validate(Data.Frames.Length);
            f.Windows = f.Windows.Validate(Data.Frames.Length);
        }
    }

    [System.Serializable]
    public class Solution {
        public double[] Values;
        public Solution() {
            Values = new double[Function.Dimensionality];
        }
    }

    [System.Serializable]
    public class Function {
        public LocalPhaseModule Module;
        public int Bone;

        public double MinSource;
        public double MaxSource;
        public double MinValue;
        public double MaxValue;
        public float localPositionMin;
        public float localPositionMax;

        public Vector3[] Pcs;
		public float[] projectedDistances;
        public float[] signedDistances;
        public Vector3 positionsMean;

        public double[] Source;
        public double[] Values;
        public double[] Fit;
        public double[] Phases;
        public double[] Amplitudes;

        public int[] Windows;

        public Dictionary<string, int[]> frameCuts = new Dictionary<string, int[]>();

        public Solution[] Solutions;

        public const int Dimensionality = 4;

        private Function Symmetric = null;

        public Function(LocalPhaseModule module, int bone) {
            Module = module;
            Bone = bone;
            Source = new double[Module.Data.GetTotalFrames()];
            Values = new double[Module.Data.GetTotalFrames()];
            Fit = new double[Module.Data.GetTotalFrames()];
            Phases = new double[Module.Data.GetTotalFrames()];
            Amplitudes = new double[Module.Data.GetTotalFrames()];
            Windows = new int[Module.Data.GetTotalFrames()];
        }

        public Function GetSymmetricFunction() {
            if(Symmetric == null) {
                int selfIndex = Module.Data.Source.FindBone(GetName()).Index;
                int otherIndex = Module.Data.Symmetry[selfIndex];
                string otherName = Module.Data.Source.Bones[otherIndex].Name;
                Symmetric = System.Array.Find(Module.Functions, x => x.GetName() == otherName);
            }
            return Symmetric == null ? this : Symmetric;
        }

        public string GetName() {
            return Module.Data.Source.Bones[Bone].Name;
        }

        public double GetSource(int index, bool normalized) {
            Source = Source.Validate(Module.Data.Frames.Length);
            return normalized ? Source[index].Normalize(MinSource, MaxSource, 0.0, 1.0) : Source[index];
        }

        public double GetValue(int index, bool normalized) {
            Values = Values.Validate(Module.Data.Frames.Length);
            return normalized ? Values[index].Normalize(MinValue, MaxValue, 0.0, 1.0) : Values[index];
        }

        public double GetFit(int index, bool normalized) {
            Fit = Fit.Validate(Module.Data.Frames.Length);
            return normalized ? Fit[index].Normalize(MinValue, MaxValue, 0.0, 1.0) : Fit[index];
        }

        public double GetPhase(int index) {
            Phases = Phases.Validate(Module.Data.Frames.Length);
            return Phases[index];
        }

        public double GetAmplitude(int index) {
            Amplitudes = Amplitudes.Validate(Module.Data.Frames.Length);
            return System.Math.Max(Amplitudes[index], 0.0);
        }

        public double GetFrequency(int index) {
            return Solutions[index].Values[1];
        }

        public double GetShift(int index) {
            return Solutions[index].Values[2];
        }

        public double GetOffset(int index) {
            return Solutions[index].Values[3];
        }

        public Vector3[] GetPcs() {
			return Pcs;
		}

		public Vector3 GetPositionsMean() {
			return positionsMean;
		}

		public float[] GetProjectedDistances(){
			return projectedDistances;
		}

        public float[] GetSignedDistances(){
			return signedDistances;
		}

        public void Preprocess() {
            // Load frame cuts for T-Pose Removal
			string Frame_CSV = "Assets/Data/Frame_Cuts.csv";
			frameCuts = new Dictionary<string, int[]>();
			string[] lines2 = File.ReadAllLines(Frame_CSV);		
			int[] frameArray;
			bool firstLine2 = true;
			foreach(string line2 in lines2){
				if(!firstLine2){
					if(line2.Split(',')[17] == "N/A"){
						frameArray = new int[16];
					}
					else if(line2.Split(',')[19] == "N/A"){
						frameArray = new int[18];
					}
					else{
						frameArray = new int[20];
					}
					for(int i=0; i<frameArray.Length; i++){
						frameArray[i] = int.Parse(line2.Split(',')[i+1]);
					}
					frameCuts.Add(line2.Split(',')[0], frameArray); // frameCuts cuts beginning and end non-stylised frames out
				}
				firstLine2 = false;			
			}
            string current_gait = Module.Data.name.Split('_')[1].Split('.')[0];
			string current_name = Module.Data.name.Split('_')[0];
            // string current_gait = "BW"; 
			// string current_name = "WalkingStickLeft"; //
            // Debug.Log(current_gait);
            // Debug.Log(current_name);
			int frameCutsIndex = 0;
            if (current_gait == "BR"){ // not neat, but it does the job for now
                frameCutsIndex = 0;
            }
            else if(current_gait == "BW"){
                frameCutsIndex = 2;
            }
            else if(current_gait == "FR"){
                frameCutsIndex = 4;
            }
            else if(current_gait == "FW"){
                frameCutsIndex = 6;
            }
            else if(current_gait == "ID"){
                frameCutsIndex = 8;
            }
            else if(current_gait == "SR"){
                frameCutsIndex = 10;
            }
            else if(current_gait == "SW"){
                frameCutsIndex = 12;
            }
            else if(current_gait == "TR" || current_gait == "TR1"){
                frameCutsIndex = 14;
            }
            else if(current_gait == "TR2"){
                frameCutsIndex = 16;
            }
            else if(current_gait == "TR3"){
                frameCutsIndex = 18;
            }
            // Debug.Log(frameCutsIndex);
			int startFrame = frameCuts[current_name][frameCutsIndex];
			int endFrame = frameCuts[current_name][frameCutsIndex+1];



            // Precalculate the principal components if required
            switch(Module.Source) {
                case SOURCE.LocalPositions:  // Get max and min outwith the T-pose
                // float[] localPositions = new float[Module.Data.GetTotalFrames()];
                localPositionMax = float.MinValue;
                localPositionMin = float.MaxValue;
                for(int i=0; i<Module.Data.GetTotalFrames(); i++) {

						int rootIdx = 0;		
                        // On mirroring - handled during exporting but using the GetPhase function which chooses the symmetric	joint to export
                        // If we mirror the data then this messes up the mean positions etc. but we only calculate the local phase using the non-mirrored version
						// then export the local phase for the correct bone based on mirror.
						// Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation((this == Module.RegularPhaseFunction ? j : Module.Data.Symmetry[j]), false).GetPosition().GetRelativePositionTo(
						// 	Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                        Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation(Bone, false).GetPosition().GetRelativePositionTo(
							Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                        // localPositions[i] = localPosition.magnitude;                        
                        if(i > startFrame && i < endFrame){
                            if(localPosition.magnitude > localPositionMax){
                                localPositionMax = localPosition.magnitude;
                            }
                            if(localPosition.magnitude < localPositionMin){
                                localPositionMin = localPosition.magnitude;
                            }
                        }
                }
                break;
                case SOURCE.PCALocalPositionsSigned: case SOURCE.PCALocalPositionsProjected:
                // PCA to create a plane for signed distance
                // By local mean relative to root not relative to parent
                Vector3[] localPositions = new Vector3[Module.Data.GetTotalFrames()];
                Pcs = new Vector3[3];
                localPositionMax = float.MinValue;
                localPositionMin = float.MaxValue;
                for(int i=0; i<Module.Data.GetTotalFrames(); i++) {

						int rootIdx = 0;
						// Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation((this == Module.RegularPhaseFunction ? j : Module.Data.Symmetry[j]), false).GetPosition().GetRelativePositionTo(
						// 	Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                        Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation(Bone, false).GetPosition().GetRelativePositionTo(
							Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                        localPositions[i] = localPosition;                        
                        if(i > startFrame && i < endFrame){
                            if(localPosition.magnitude > localPositionMax){
                                localPositionMax = localPosition.magnitude;
                            }
                            if(localPosition.magnitude < localPositionMin){
                                localPositionMin = localPosition.magnitude;
                            }
                        }
                }
                // First cut away unused frames - can make more efficient by just making cutPositions, don't need to store localPositions
                Vector3[] cutPositions = new Vector3[endFrame - startFrame - 1];
                int cutPosIdx = 0;
                for(int i=0; i<localPositions.Length; i++){
                    if(i > startFrame && i < endFrame){
                        cutPositions[cutPosIdx] = localPositions[i];
                        cutPosIdx += 1;
                    }
                }

                // TODO (opt): cleaning. Put the PCA calculation into utility takes in cutPositions
                // Implement PCA - put into Matrix for Eigen
                Matrix M = new Matrix(cutPositions.Length, 3, "position_matrix");
                for(int i=0; i<cutPositions.Length; i++){
                    M.SetValue(i, 0, cutPositions[i].x);
                    M.SetValue(i, 1, cutPositions[i].y);
                    M.SetValue(i, 2, cutPositions[i].z);
                }

                // Standardize data 
                float meanx = M.ColMean(0);
                float meany = M.ColMean(1);
                float meanz = M.ColMean(2);
                positionsMean = new Vector3(meanx, meany, meanz);
                //  Do not standardize by std as all our data is on same scale and we want to capture this variance
                // float stdx = M.ColStd(0);
                // float stdy = M.ColStd(1);
                // float stdz = M.ColStd(2);

                Matrix centredM = new Matrix(cutPositions.Length, 3, "standardised_position_matrix");
                // for(int i=0; i<cutPositions.Length; i++){
                // 	centredM.SetValue(i, 0, (M.GetValue(i, 0) - meanx) / stdx);
                // 	centredM.SetValue(i, 1, (M.GetValue(i, 1) - meany) / stdy);
                // 	centredM.SetValue(i, 2, (M.GetValue(i, 2) - meanz) / stdz);
                // }
                for(int i=0; i<cutPositions.Length; i++){
                    centredM.SetValue(i, 0, (M.GetValue(i, 0) - meanx));
                    centredM.SetValue(i, 1, (M.GetValue(i, 1) - meany));
                    centredM.SetValue(i, 2, (M.GetValue(i, 2) - meanz));
                }

                // Compute covariance matrix
                Matrix transposedM = new Matrix(3, cutPositions.Length, "transposed_standardised_position_matrix");
                for(int i=0; i<cutPositions.Length; i++){
                    transposedM.SetValue(0, i, centredM.GetValue(i, 0));
                    transposedM.SetValue(1, i, centredM.GetValue(i, 1));
                    transposedM.SetValue(2, i, centredM.GetValue(i, 2));
                }
                Matrix covarianceM = new Matrix(3, 3, "covariance_matrix");
                Matrix.Product(transposedM, centredM, covarianceM);
                Matrix.Scale(covarianceM, 1f / (cutPositions.Length - 1), covarianceM);
                
                // Debug.Log("PRINT COVARIANCE MATRIX - before and after scaling - looks reasonable?");
                // Debug.Log("Check it is symmetric too");
                // for(int i=0; i<3; i++){
                // 	for(int j=0; j<3; j++){
                // 		Debug.Log(covarianceM.GetValue(i,j));
                // 	}
                // }                

                // Since covariance is symettric we can use the SelfAdjointEigenSolver
                // Compute eigenvectors and eigenvalues of covariance matrix
                Matrix eigenValues = new Matrix(3, 1, "eigen_value_matrix");
                Matrix eigenVectors = new Matrix(3, 3, "eigen_vector_matrix");
                Matrix.SelfAdjointEigenSolver3x3(covarianceM, eigenValues, eigenVectors);

                // Debug.Log("Eigen Values");
                // Debug.Log(eigenValues.GetValue(0, 0));
                // Debug.Log(eigenValues.GetValue(1, 0));
                // Debug.Log(eigenValues.GetValue(2, 0));
                // Debug.Log("Eigen Vectors");
                // for(int i=0; i<3; i++){
                // 	for(int j=0; j<3; j++){
                // 		Debug.Log(eigenVectors.GetValue(i,j));
                // 	}
                // }

                // Columns of eigenVectors are the eigenvectors corresponding to the eigen values which are sorted small -> large
                // So for example we want the first two columns of eigenVectors for the last two principal components
                // These 2 columns will give us a plane orthogonal to the first principal component which when we cross we want to change the sign of the signed distance
                Pcs[0] = new Vector3(eigenVectors.GetValue(0, 2), eigenVectors.GetValue(1, 2), eigenVectors.GetValue(2, 2));  // first PC
                Pcs[1] = new Vector3(eigenVectors.GetValue(0, 1), eigenVectors.GetValue(1, 1), eigenVectors.GetValue(2, 1));  // second PC
                Pcs[2] = new Vector3(eigenVectors.GetValue(0, 0), eigenVectors.GetValue(1, 0), eigenVectors.GetValue(2, 0));  // third PC

                // There is a potential issue where the principal components can point in either direction (forward/backward) but we want it to be directed
                // As our co-ordinate systems is relative to the root we can direct the PCs. We just need to ensure a consistent directionality
                // Debug.Log(Pcs[0]);
                // Debug.Log("#####");
                // Debug.Log(Pcs[0]);
                // Debug.Log(eigenValues.GetValue(2, 0));
                // Debug.Log(eigenValues.GetValue(1, 0));
                // Debug.Log(eigenValues.GetValue(0, 0));
                if(Pcs[0].z < 0){
                    Pcs[0] *= -1f;
                }
                if(Pcs[0].z == 0){
                    Debug.Log("It should be very unlikely that principal comp. is parallel to character forward - are you sure the set up is correct?");
                    if(Pcs[0].x <= 0){
                        Pcs[0] *= -1f;
                    }
                }
                // Debug.Log(Pcs[0]);
                // Debug.Log("#####");
            
                // To project onto first principal component, dot the standardised vectors with the principal component vector
                // This in turn can be used to get the sign of the signed distance
                // Do we need to redo max and min trimming of velocities?
                projectedDistances = new float[Module.Data.GetTotalFrames()];
                for(int i=0; i<projectedDistances.Length; i++){
                    if(i > startFrame && i < endFrame){
                        projectedDistances[i] = Vector3.Dot(new Vector3(centredM.GetValue(i - startFrame - 1, 0), centredM.GetValue(i - startFrame - 1, 1), centredM.GetValue(i - startFrame - 1, 2)), Pcs[0]);  //standardised
                        // Debug.Log(projectedDistances[i]);
                    }
                    else{
                        projectedDistances[i] = 0f;
                    }
                }
                
                int positiveCount = 0;
                int negativeCount = 0;
                for(int i=0; i<projectedDistances.Length; i++){
                    if(i > startFrame && i < endFrame){
                        if(Mathf.Sign(projectedDistances[i]) > 0){
                            positiveCount += 1;
                        }
                        else{
                            negativeCount += 1;
                        }
                    }
                }
                // Debug.Log("Percentage of projections above 0: " + 100f * (float)positiveCount / (float)(endFrame - startFrame - 1));
                // Debug.Log("Percentage of projections below or equal 0: " + 100f * (float)negativeCount / (float)(endFrame - startFrame - 1));

                signedDistances = new float[projectedDistances.Length];
                for(int i=0; i<signedDistances.Length; i++){
                    if(i > startFrame && i < endFrame){
                        signedDistances[i] = new Vector3(centredM.GetValue(i - startFrame - 1, 0), centredM.GetValue(i - startFrame - 1, 1), centredM.GetValue(i - startFrame - 1, 2)).magnitude;
                        signedDistances[i] = signedDistances[i] * Mathf.Sign(projectedDistances[i]);
                    }
                    else{
                        signedDistances[i] = projectedDistances[i];
                    }
                }

                break;
                case SOURCE.Hybrid:
                // Note: if only leave hybrid as an option need to add back the types of various variables as the type is defined in the 
                // case above (same scope)
                if((Bone == 21 || Bone == 22 || Bone == 26 || Bone == 27) && current_name != "LeftHop" && current_name != "RightHop"){ //hardcoding the feet to use contact information
                    // Get contacts from contact module and return them
                    ContactModule ContactMod = (ContactModule)Module.Data.GetModule((Module.TYPE)(4));
                    // Requires that contact module is initialised before local phase added
                    // Debug.Log("No contact module initialised - required for hybrid local phase labelling");

                    projectedDistances = new float[Module.Data.GetTotalFrames()];
                    float contact;
                    for(int i=0; i<projectedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            if(Bone == 21 || Bone == 22){
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[0];
                            }
                            else{
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[1];
                            }      
                            projectedDistances[i] = contact; 
                        }
                        else{
                            projectedDistances[i] = 0f;
                        }
                    }
                }
                else if(current_name == "LeftHop" && (Bone == 26 || Bone == 27)){ //hardcoding the feet to use contact information
                    // Get contacts from contact module and return them
                    ContactModule ContactMod = (ContactModule)Module.Data.GetModule((Module.TYPE)(4));
                    // Requires that contact module is initialised before local phase added
                    // Debug.Log("No contact module initialised - required for hybrid local phase labelling");

                    projectedDistances = new float[Module.Data.GetTotalFrames()];
                    float contact;
                    for(int i=0; i<projectedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            if(Bone == 21 || Bone == 22){
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[0];
                            }
                            else{
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[1];
                            }      
                            projectedDistances[i] = contact; 
                        }
                        else{
                            projectedDistances[i] = 0f;
                        }
                    }
                }
                else if(current_name == "RightHop" && (Bone == 21 || Bone == 22)){ //hardcoding the feet to use contact information
                    // Get contacts from contact module and return them
                    ContactModule ContactMod = (ContactModule)Module.Data.GetModule((Module.TYPE)(4));
                    // Requires that contact module is initialised before local phase added
                    // Debug.Log("No contact module initialised - required for hybrid local phase labelling");

                    projectedDistances = new float[Module.Data.GetTotalFrames()];
                    float contact;
                    for(int i=0; i<projectedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            if(Bone == 21 || Bone == 22){
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[0];
                            }
                            else{
                                contact = ContactMod.GetFootContacts(Module.Data.GetFrame(i), false)[1];
                            }      
                            projectedDistances[i] = contact; 
                        }
                        else{
                            projectedDistances[i] = 0f;
                        }
                    }
                }
                else{
                    // PCA to create a plane for signed distance
                    // By local mean relative to root not relative to parent
                    localPositions = new Vector3[Module.Data.GetTotalFrames()];
                    Pcs = new Vector3[3];
                    localPositionMax = float.MinValue;
                    localPositionMin = float.MaxValue;
                    for(int i=0; i<Module.Data.GetTotalFrames(); i++) {

                            int rootIdx = 0;
                            // Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation((this == Module.RegularPhaseFunction ? j : Module.Data.Symmetry[j]), false).GetPosition().GetRelativePositionTo(
                            // 	Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                            Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation(Bone, false).GetPosition().GetRelativePositionTo(
                                Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                            localPositions[i] = localPosition;                        
                            if(i > startFrame && i < endFrame){
                                if(localPosition.magnitude > localPositionMax){
                                    localPositionMax = localPosition.magnitude;
                                }
                                if(localPosition.magnitude < localPositionMin){
                                    localPositionMin = localPosition.magnitude;
                                }
                            }
                    }
                    // First cut away unused frames - can make more efficient by just making cutPositions, don't need to store localPositions
                    cutPositions = new Vector3[endFrame - startFrame - 1];
                    cutPosIdx = 0;
                    for(int i=0; i<localPositions.Length; i++){
                        if(i > startFrame && i < endFrame){
                            cutPositions[cutPosIdx] = localPositions[i];
                            cutPosIdx += 1;
                        }
                    }

                    // TODO (opt): cleaning. Put the PCA calculation into utility takes in cutPositions
                    // Implement PCA - put into Matrix for Eigen
                    M = new Matrix(cutPositions.Length, 3, "position_matrix");
                    for(int i=0; i<cutPositions.Length; i++){
                        M.SetValue(i, 0, cutPositions[i].x);
                        M.SetValue(i, 1, cutPositions[i].y);
                        M.SetValue(i, 2, cutPositions[i].z);
                    }

                    // Standardize data 
                    meanx = M.ColMean(0);
                    meany = M.ColMean(1);
                    meanz = M.ColMean(2);
                    positionsMean = new Vector3(meanx, meany, meanz);
                    //  Do not standardize by std as all our data is on same scale and we want to capture this variance
                    // float stdx = M.ColStd(0);
                    // float stdy = M.ColStd(1);
                    // float stdz = M.ColStd(2);

                    centredM = new Matrix(cutPositions.Length, 3, "standardised_position_matrix");
                    for(int i=0; i<cutPositions.Length; i++){
                        centredM.SetValue(i, 0, (M.GetValue(i, 0) - meanx));
                        centredM.SetValue(i, 1, (M.GetValue(i, 1) - meany));
                        centredM.SetValue(i, 2, (M.GetValue(i, 2) - meanz));
                    }

                    // Compute covariance matrix
                    transposedM = new Matrix(3, cutPositions.Length, "transposed_standardised_position_matrix");
                    for(int i=0; i<cutPositions.Length; i++){
                        transposedM.SetValue(0, i, centredM.GetValue(i, 0));
                        transposedM.SetValue(1, i, centredM.GetValue(i, 1));
                        transposedM.SetValue(2, i, centredM.GetValue(i, 2));
                    }
                    covarianceM = new Matrix(3, 3, "covariance_matrix");
                    Matrix.Product(transposedM, centredM, covarianceM);
                    Matrix.Scale(covarianceM, 1f / (cutPositions.Length - 1), covarianceM);  

                    // Since covariance is symettric we can use the SelfAdjointEigenSolver
                    // Compute eigenvectors and eigenvalues of covariance matrix
                    eigenValues = new Matrix(3, 1, "eigen_value_matrix");
                    eigenVectors = new Matrix(3, 3, "eigen_vector_matrix");
                    Matrix.SelfAdjointEigenSolver3x3(covarianceM, eigenValues, eigenVectors);

                    // Columns of eigenVectors are the eigenvectors corresponding to the eigen values which are sorted small -> large
                    // So for example we want the first two columns of eigenVectors for the last two principal components
                    // These 2 columns will give us a plane orthogonal to the first principal component which when we cross we want to change the sign of the signed distance
                    Pcs[0] = new Vector3(eigenVectors.GetValue(0, 2), eigenVectors.GetValue(1, 2), eigenVectors.GetValue(2, 2));  // first PC
                    Pcs[1] = new Vector3(eigenVectors.GetValue(0, 1), eigenVectors.GetValue(1, 1), eigenVectors.GetValue(2, 1));  // second PC
                    Pcs[2] = new Vector3(eigenVectors.GetValue(0, 0), eigenVectors.GetValue(1, 0), eigenVectors.GetValue(2, 0));  // third PC

                    // There is a potential issue where the principal components can point in either direction (forward/backward) but we want it to be directed
                    // As our co-ordinate systems is relative to the root we can direct the PCs. We just need to ensure a consistent directionality
                    // Debug.Log(Pcs[0]);
                    if(Pcs[0].z < 0){
                        Pcs[0] *= -1f;
                    }
                    if(Pcs[0].z == 0){
                        Debug.Log("It should be very unlikely that principal comp. is parallel to character forward - are you sure the set up is correct?");
                        if(Pcs[0].x <= 0){
                            Pcs[0] *= -1f;
                        }
                    }
                    // Debug.Log(Pcs[0]);
                    // Debug.Log("#####");
                
                    // To project onto first principal component, dot the standardised vectors with the principal component vector
                    // This in turn can be used to get the sign of the signed distance
                    // Do we need to redo max and min trimming of velocities?
                    projectedDistances = new float[Module.Data.GetTotalFrames()];
                    for(int i=0; i<projectedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            projectedDistances[i] = Vector3.Dot(new Vector3(centredM.GetValue(i - startFrame - 1, 0), centredM.GetValue(i - startFrame - 1, 1), centredM.GetValue(i - startFrame - 1, 2)), Pcs[0]);  //standardised
                            // Debug.Log(projectedDistances[i]);
                        }
                        else{
                            projectedDistances[i] = 0f;
                        }
                    }
                    
                    positiveCount = 0;
                    negativeCount = 0;
                    for(int i=0; i<projectedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            if(Mathf.Sign(projectedDistances[i]) > 0){
                                positiveCount += 1;
                            }
                            else{
                                negativeCount += 1;
                            }
                        }
                    }
                    // Debug.Log("Percentage of projections above 0: " + 100f * (float)positiveCount / (float)(endFrame - startFrame - 1));
                    // Debug.Log("Percentage of projections below or equal 0: " + 100f * (float)negativeCount / (float)(endFrame - startFrame - 1));

                    signedDistances = new float[projectedDistances.Length];
                    for(int i=0; i<signedDistances.Length; i++){
                        if(i > startFrame && i < endFrame){
                            signedDistances[i] = new Vector3(centredM.GetValue(i - startFrame - 1, 0), centredM.GetValue(i - startFrame - 1, 1), centredM.GetValue(i - startFrame - 1, 2)).magnitude;
                            signedDistances[i] = signedDistances[i] * Mathf.Sign(projectedDistances[i]);
                        }
                        else{
                            signedDistances[i] = projectedDistances[i];
                        }
                    }
                }
                break;
            }

            //Start Processing
            for(int i=0; i<Source.Length; i++) {
                Source[i] = GetSource(Module.Data.Frames[i]);
                float GetSignal(Frame frame) {
                    int rootIdx = 0;
                    switch(Module.Source) {
                        case SOURCE.LocalPositions:
                        // Vector3 localPosition = Module.Data.Frames[i].GetBoneTransformation((this == Module.RegularPhaseFunction ? j : Module.Data.Symmetry[j]), false).GetPosition().GetRelativePositionTo(
						// 	Module.Data.Frames[i].GetBoneTransformation(rootIdx, false));
                        Vector3 localPosition = frame.GetBoneTransformation(Bone, false).GetPosition().GetRelativePositionTo(frame.GetBoneTransformation(rootIdx, false));
                        if(frame.Index < startFrame || frame.Index > endFrame){
                            return ((localPositionMax + localPositionMin) / 2f);
                        }
                        else{
                            return localPosition.magnitude;
                        }

                        case SOURCE.LocalVelocities:
                        if(frame.Index < startFrame || frame.Index > endFrame){
                            return (0f);
                        }
                        else{
                            return frame.GetBoneLocalVelocity(Bone, 0, false).magnitude;
                        }

                        case SOURCE.Velocities:
                        // float boneVelocity = Mathf.Min(Module.Data.Frames[i].GetBoneVelocity(j,  (this == Module.RegularPhaseFunction ? false : true)).magnitude, Module.MaximumVelocity);
                        if(frame.Index < startFrame || frame.Index > endFrame){
                            return (0f);
                        }
                        else{
                            return frame.GetBoneVelocity(Bone, false).magnitude;
                        }
                        

                        case SOURCE.PCALocalPositionsSigned:
                        // Try signed distance from mean distance (so take distance from mean distance and make + if in front of plane and - if behind)
                        // Not checked carefully if this is correct, just approximated to get a feel                    
                        return signedDistances[frame.Index - 1];

                        case SOURCE.PCALocalPositionsProjected:
                        // Try simply the projected component. Since Pcs are unit vectors this is the perpendicular distance to the plane.
                        return projectedDistances[frame.Index - 1];

                        case SOURCE.Hybrid:                   
                        return projectedDistances[frame.Index - 1];


                        default:
                        return 0f;
                    }
                }
                float GetSource(Frame frame) {
                    if(Module.ApplyNormalization) {  // Normalizes the data using the values in the current window
                        float[] timestamps = Module.Data.SimulateTimestamps(frame, 1f/2f, 1f/2f); //HARDCODED Window = 1s
                        float[] signals = new float[timestamps.Length];
                        for(int t=0; t<timestamps.Length; t++) {
                           if(timestamps[t] < 0f){
                               signals[t] = GetSignal(Module.Data.GetFirstFrame());
                           }
                           else if(timestamps[t] > Module.Data.GetLastFrame().Timestamp){
                               signals[t] = GetSignal(Module.Data.GetLastFrame());
                           }
                           else{
                               signals[t] = GetSignal(Module.Data.GetFrame(timestamps[t]));
                           }
                        }
                        float c = GetSignal(frame);
                        float mean = signals.Mean();
                        float std = signals.Sigma();
                        std = std == 0f ? 1f : std;
                        return (c-mean) / std;
                    } else {
                        return GetSignal(frame);
                    }
                }
            }

            if(Module.ApplyButterworth) { 
                Values = Utility.Butterworth(Source, (double)Module.Data.GetDeltaTime(), (double)Module.MaxFrequency);
            }

            for(int i=0; i<Values.Length; i++) {
                if(System.Math.Abs(Values[i]) < 1e-3) {
                    Values[i] = 0.0;
                }
            }

            // TODO (opt): do we need to do anything more than this, like in the contacts case below, or is this okay?
            // Not sure what it does
            Windows.SetAll(30); 
            //Refine Windows
            // switch(Module.Source) {
            //     case SOURCE.Contact:
            //     {
            //         for(int i=0; i<Values.Length; i++) {
            //             int window = Module.Asset.GetModule<ContactModule>().GetSensor(GetName()).GetStateWindow(Module.Asset.Frames[i], false);
            //             float active = 1f - Values.GatherByOverflowWindow(i, window / 2).Ratio(0.0);
            //             Windows[i] = Mathf.RoundToInt(active * window);
            //         }
            //         int[] tmp = new int[Values.Length];
            //         for(int i=0; i<Values.Length; i++) {
            //             int[] windows = Windows.GatherByOverflowWindow(i, 30);      //HARDCODED Window = 1s
            //             double[] values = Values.GatherByOverflowWindow(i, 30);     //HARDCODED Window = 1s
            //             bool[] mask = new bool[values.Length];
            //             for(int j=0; j<values.Length; j++) {
            //                 mask[j] = values[j] != 0f;
            //             }
            //             tmp[i] = Mathf.RoundToInt(windows.Gaussian(mask));
            //         }
            //         Windows = tmp;
            //     }
            //     break;

            //     case SOURCE.Velocity:
            //     Windows.SetAll(30);
            //     break;

            //     case SOURCE.Interaction:
            //     Windows.SetAll(30);
            //     break;
            // }

            MinSource = Source.Min();
            MaxSource = Source.Max();
            MinValue = Values.Min();
            MaxValue = Values.Max();
        }

        public void Optimize(ref bool token, ref bool[] threads, ref int[] iterations, ref float[] progress, int thread) {
            Solutions = new Solution[Values.Length];
            for(int i=0; i<Solutions.Length; i++) {
                Solutions[i] = new Solution();
            }
            Population[] populations = new Population[Values.Length];
            for(int i=0; i<Values.Length; i++) {
                int padding = Windows[i] / 2;
                Frame[] frames = Module.Data.Frames.GatherByOverflowWindow(i, padding);  // TODO (opt) - what/where is GatherByOverflowWindow?
                double[] values = Values.GatherByOverflowWindow(i, padding);
                if(values.AbsSum() < 1e-3) {
                    continue;
                }
                double min = values.Min();
                double max = values.Max();
                double[] lowerBounds = new double[Dimensionality]{0.0, 1f/Module.MaxFrequency, -0.5, min};
                double[] upperBounds = new double[Dimensionality]{max-min, Module.MaxFrequency, 0.5, max};
                double[] seed = new double[Dimensionality];
                for(int j=0; j<Dimensionality; j++) {
                    seed[j] = 0.5 * (lowerBounds[j] + upperBounds[j]);
                }
                double[] _g = new double[Dimensionality];
                double[] _tmp = new double[Dimensionality];
                Interval _interval = new Interval(frames.First().Index, frames.Last().Index);
                System.Func<double[], double> func = x => Loss(_interval, x);
                System.Func<double[], double[]> grad = g => Grad(_interval, g, 0.1, _g, _tmp);
                populations[i] = new Population(this, Module.Individuals, Module.Elites, Module.Exploration, Module.Memetism, lowerBounds, upperBounds, seed, func, grad);
            }

            iterations[thread] = 0;
            while(!token) {
                //Iterate
                for(int i=0; i<populations.Length && !token; i++) {
                    if(populations[i] != null) {
                        populations[i].Evolve();
                        Solutions[i].Values = populations[i].GetSolution();
                    }
                    progress[thread] = (float)(i+1) / (float)populations.Length;
                }
                Compute(false);
                iterations[thread] += 1;
                if(Module.MaxIterations > 0 && iterations[thread] >= Module.MaxIterations) {
                    break;
                }
            }
            Compute(true);
            threads[thread] = false;
        }

        public void Compute(bool postprocess) {
            if(Solutions == null || Solutions.Length != Values.Length || Solutions.Any(null)) {
                Debug.Log("Computing failed because no solutions are available.");
                return;
            }
            for(int i=0; i<Values.Length; i++) {
                // Windows[i] = GetPhaseWindow(i);
                Fit[i] = ComputeFit(i);
                Phases[i] = ComputePhase(i);
                Amplitudes[i] = ComputeAmplitude(i);
            }
            if(postprocess) {
                double[] px = new double[Phases.Length];
                double[] py = new double[Phases.Length];
                for(int i=0; i<Phases.Length; i++) {
                    Vector2 v = Utility.PhaseVector((float)Phases[i]);  //TODO (opt): do we have this utility function
                    px[i] = v.x;
                    py[i] = v.y;
                }
                if(Module.ApplyButterworth) {
                    px = Utility.Butterworth(px, (double)Module.Data.GetDeltaTime(), (double)Module.MaxFrequency);  //TODO (opt): do we have this utility function
                    py = Utility.Butterworth(py, (double)Module.Data.GetDeltaTime(), (double)Module.MaxFrequency);
                }
                for(int i=0; i<Phases.Length; i++) {
                    Phases[i] = Utility.PhaseValue(new Vector2((float)px[i], (float)py[i]).normalized);  //TODO (opt): do we have this utility function
                }
                if(Module.ApplyButterworth) {
                    Amplitudes = Utility.Butterworth(Amplitudes, (double)Module.Data.GetDeltaTime(), (double)Module.MaxFrequency);
                }
            }

            double ComputeFit(int index) {
                return Trigonometric(index, Solutions[index].Values);
            }

            double ComputePhase(int index) {
                double[] x = Solutions[index].Values;
                double t = Module.Data.Frames[index].Timestamp;
                double F = x[1];
                double S = x[2];
                return Mathf.Repeat((float)(F * t - S), 1f);
            }
            
            double ComputeAmplitude(int index) {
                // Debug.Log(Solutions[index].Values[0]);
                // return Solutions[index].Values[0];
                // return 1f;

                // Frame[] window = Module.Data.Frames.GatherByOverflowWindow(index, GetPhaseWindow(index) / 2);
                // float max_vel = 0f;
                // foreach(Frame frame in window){
                //     if(frame.GetBoneVelocity(Bone, false).magnitude > max_vel){
                //         max_vel = frame.GetBoneVelocity(Bone, false).magnitude;
                //     }
                //     // if(frame.GetBoneLocalVelocity(Bone, 0, false).magnitude > max_vel){
                //     //     max_vel = frame.GetBoneLocalVelocity(Bone, 0, false).magnitude;
                //     // }
                // }
                // // Debug.Log(window.Length);

                // // float max_vel = Module.Data.Frames[index].GetBoneLocalVelocity(Bone, 0, false).magnitude;


                // // Debug.Log("#####");
                // // Debug.Log(max_vel);
                // // Debug.Log("#####");
                // return max_vel;


                Frame[] window = Module.Data.Frames.GatherByOverflowWindow(index, GetPhaseWindow(index) / 2);
                return Solutions[index].Values[0] * GetMaxVelocityMagnitude(window);  // amplitude * max_velocity_in_window

                // Module.Data.Frames[index].GetBoneVelocity(Bone, false).magnitude;
                //TODO (opt): design Amplitudes to do something sensible when multiplied with phase
                // return (double)Module.Data.Frames[index].GetBoneVelocity(Bone, false).magnitude;                // TODO (opt): if do something clever with windows may want to consider the below
                // switch(Module.Source) {
                //     case SOURCE.Contact:
                //     Frame[] window = Module.Data.Frames.GatherByOverflowWindow(index, GetPhaseWindow(index) / 2);
                //     return GetSmoothedAmplitude(window) * GetMaxVelocityMagnitude(window);

                //     case SOURCE.Velocity:
                //     return Solutions[index].Values[0];

                //     case SOURCE.Interaction:
                //     return Solutions[index].Values[0];

                //     default:
                //     return 0f;
                // }
            }
        }

        public int GetPhaseWindow(int index) {
            if(Solutions == null || Solutions.Length != Values.Length || Solutions.Any(null)) {
                return 0;
            }
            float f = (float)Solutions[index].Values[1];
            return f == 0f ? 0 : Mathf.RoundToInt(Module.Data.Framerate / f);
        }

        // TODO (opt): is this function used?
        // private double GetSmoothedAmplitude(Frame[] frames) {
		// 	double[] values = new double[frames.Length];
		// 	for(int i=0; i<frames.Length; i++) {
		// 		values[i] = Solutions[frames[i].Index-1].Values[0];
		// 	}
        //     return values.Gaussian();
        // }

        // TODO (opt): Figure out what is going on here with the LocalVelocities flag, likely we don't need the option but which do we want?
        private float GetMaxVelocityMagnitude(Frame[] frames) {
            if(!Module.LocalVelocities) {
                float magnitude = 0f;
                for(int i=0; i<frames.Length; i++) {
                    magnitude = Mathf.Max(frames[i].GetBoneVelocity(Bone, false).magnitude, magnitude);
                }
                return magnitude;
            } else {
                float magnitude = 0f;
                for(int i=0; i<frames.Length; i++) {
                    magnitude = Mathf.Max(frames[i].GetBoneLocalVelocity(Bone, 0, false).magnitude, magnitude);
                }
                return magnitude;
            }
        }

        private double Trigonometric(int index, double[] x) {
            double t = Module.Data.Frames[index].Timestamp;
            double A = x[0];
            double F = x[1];
            double S = x[2];
            double B = x[3];
            return A * System.Math.Sin(2.0*System.Math.PI * (F * t - S)) + B;
        }

        private double Loss(Interval interval, double[] x) {
            double loss = 0.0;
            double count = 0.0;
            for(int i=interval.Start; i<=interval.End; i++) {
                Accumulate(i);
            }
            int padding = interval.GetLength() / 2;
            for(int i=1; i<=padding; i++) {
                double w = 1.0 - (double)(i)/(double)(padding); //Mean
                // double w = Mathf.Exp(-Mathf.Pow((float)i - (float)padding, 2f) / Mathf.Pow(0.5f * (float)padding, 2f)); //Gaussian
                w *= w;
                Connect(interval.Start - i, w);
                Connect(interval.End + i, w);
            }

            loss /= count;
            loss = System.Math.Sqrt(loss);

            return loss;

            void Accumulate(int frame) {
                if(frame >= 1 && frame <= Values.Length) {
                    double error = Values[frame-1] - Trigonometric(frame-1, x);
                    error *= error;
                    loss += error;
                    count += 1.0;
                }
            }

            void Connect(int frame, double weight) {
                if(frame >= 1 && frame <= Values.Length) {
                    // double weight = System.Math.Abs(Values[frame-1]);
                    double error = Fit[frame-1] - Trigonometric(frame-1, x);
                    error *= error;
                    loss += weight * error;
                    count += weight;
                }
            }
        }

        private double[] Grad(Interval interval, double[] x, double delta, double[] grad, double[] tmp) {
            for(int i=0; i<x.Length; i++) {
                tmp[i] = x[i];
            }
            double loss = Loss(interval, tmp);
            for(int i=0; i<tmp.Length; i++) {
                tmp[i] += delta;
                grad[i] = (Loss(interval, tmp) - loss) / delta;
                tmp[i] -= delta;
            }
            return grad;
        }

        private class Population {

            private Function Function;
            private System.Random RNG;

            private int Size;
            private int Elites;
            private int Dimensionality;
            private double Exploration;
            private double Memetism;
            public double[] LowerBounds;
            public double[] UpperBounds;

            private double[] RankProbabilities;
            private double RankProbabilitySum;

            private System.Func<double[], double> Func;
            private System.Func<double[], double[]> Grad;

            private Individual[] Individuals;
            private Individual[] Offspring;
            
            // private Accord.Math.Optimization.Cobyla Memetic;

            public Population(Function function, int size, int elites, float exploration, float memetism, double[] lowerBounds, double[] upperBounds, double[] seed, System.Func<double[], double> func, System.Func<double[], double[]> grad) {
                Function = function;
                RNG = new System.Random();

                Size = size;
                Elites = elites;
                Dimensionality = seed.Length;
                Exploration = exploration;
                Memetism = memetism;

                LowerBounds = lowerBounds;
                UpperBounds = upperBounds;

                Func = func;
                Grad = grad;

                // Setup Memetic  - No longer using as requires Accord and not essential
                // Memetic = new Accord.Math.Optimization.Cobyla(Dimensionality, Func);
                // Memetic.MaxIterations = 10;

                //Compute rank probabilities
                double rankSum = (double)(Size*(Size+1)) / 2.0;
                RankProbabilities = new double[Size];
                for(int i=0; i<Size; i++) {
                    RankProbabilities[i] = (double)(Size-i)/rankSum;
                    RankProbabilitySum += RankProbabilities[i];
                }

                //Create population
                Individuals = new Individual[Size];
                Offspring = new Individual[Size];
                for(int i=0; i<size; i++) {
                    Individuals[i] = new Individual(Dimensionality);
                    Offspring[i] = new Individual(Dimensionality);
                }

                //Initialise randomly
                Individuals[0].Genes = (double[])seed.Clone();
                for(int i=1; i<size; i++) {
                    Reroll(Individuals[i]);
                }

                //Finalise
                EvaluateFitness(Offspring);
                SortByFitness(Offspring);
                AssignExtinctions(Offspring);
            }

            public double[] GetSolution() {
                return Individuals[0].Genes;
            }

             public void Evolve() {
                //Copy elite
                for(int i=0; i<Elites; i++) {
                    Copy(Individuals[i], Offspring[i]);                    
                }

                //Remaining individuals
                for(int o=Elites; o<Size; o++) {
                    Individual child = Offspring[o];
                    if(GetRandom() <= 1.0-Exploration) {
                        Individual parentA = Select(Individuals);
                        Individual parentB = Select(Individuals);
                        while(parentB == parentA) {
                            parentB = Select(Individuals);
                        }
                        Individual prototype = Select(Individuals);
                        while(prototype == parentA || prototype == parentB) {
                            prototype = Select(Individuals);
                        }

                        double mutationRate = GetMutationProbability(parentA, parentB);
                        double mutationStrength = GetMutationStrength(parentA, parentB);

                        for(int i=0; i<Dimensionality; i++) {
                            double weight;

                            //Recombination
                            weight = GetRandom();
                            double momentum = GetRandom() * parentA.Momentum[i] + GetRandom() * parentB.Momentum[i];
                            if(GetRandom() < 0.5) {
                                child.Genes[i] = parentA.Genes[i] + momentum;
                            } else {
                                child.Genes[i] = parentB.Genes[i] + momentum;
                            }

                            //Store
                            double gene = child.Genes[i];

                            //Mutation
                            if(GetRandom() <= mutationRate) {
                                double span = UpperBounds[i] - LowerBounds[i];
                                child.Genes[i] += GetRandom(-mutationStrength*span, mutationStrength*span);
                            }
                            
                            //Adoption
                            weight = GetRandom();
                            child.Genes[i] += 
                                weight * GetRandom() * (0.5f * (parentA.Genes[i] + parentB.Genes[i]) - child.Genes[i])
                                + (1.0-weight) * GetRandom() * (prototype.Genes[i] - child.Genes[i]);

                            //Clamp
                            if(child.Genes[i] < LowerBounds[i]) {
                                child.Genes[i] = LowerBounds[i];
                            }
                            if(child.Genes[i] > UpperBounds[i]) {
                                child.Genes[i] = UpperBounds[i];
                            }

                            //Momentum
                            child.Momentum[i] = GetRandom() * momentum + (child.Genes[i] - gene);
                        }
                    } else {
                        Reroll(child);
                    }
                }

                //Memetic Local Search - No longer using as requires Accord and not essential
                // for(int i=0; i<Offspring.Length; i++) {
                //     if(GetRandom() <= Memetism) {
                //         Memetic.Minimize(Offspring[i].Genes);
                //         for(int j=0; j<Memetic.Solution.Length; j++) {
                //             if(Memetic.Solution[j] < LowerBounds[j]) {
                //                 Memetic.Solution[j] = LowerBounds[j];
                //             }
                //             if(Memetic.Solution[j] > UpperBounds[j]) {
                //                 Memetic.Solution[j] = UpperBounds[j];
                //             }
                //             Offspring[i].Momentum[j] = Memetic.Solution[j] - Offspring[i].Genes[j];
                //             Offspring[i].Genes[j] = Memetic.Solution[j];
                //         }
                //     }
                // }

                //Finalise
                EvaluateFitness(Offspring);
                SortByFitness(Offspring);
                AssignExtinctions(Offspring);

                //Update
                Utility.Swap(ref Individuals, ref Offspring);
            }

            private double GetRandom(double min=0.0, double max=1.0) { 
                return RNG.NextDouble() * (max - min) + min;
            }

            //Copies an individual from from to to
			private void Copy(Individual from, Individual to) {
				for(int i=0; i<Dimensionality; i++) {
					to.Genes[i] = from.Genes[i];
					to.Momentum[i] = from.Momentum[i];
				}
				to.Extinction = from.Extinction;
				to.Fitness = from.Fitness;
			}

            //Rerolls an individual
			private void Reroll(Individual individual) {
				for(int i=0; i<Dimensionality; i++) {
					individual.Genes[i] = GetRandom(LowerBounds[i], UpperBounds[i]);
                    individual.Momentum[i] = 0.0;
				}
			}

            //Rank-based selection of an individual
            private Individual Select(Individual[] entities) {
                double rVal = GetRandom() * RankProbabilitySum;
                for(int i=0; i<Size; i++) {
                    rVal -= RankProbabilities[i];
                    if(rVal <= 0.0) {
                        return entities[i];
                    }
                }
                return entities[Size-1];
            }

            //Returns the mutation probability from two parents
			private double GetMutationProbability(Individual parentA, Individual parentB) {
				double extinction = 0.5 * (parentA.Extinction + parentB.Extinction);
				double inverse = 1.0/(double)Dimensionality;
				return extinction * (1.0-inverse) + inverse;
			}

			//Returns the mutation strength from two parents
			private double GetMutationStrength(Individual parentA, Individual parentB) {
				return 0.5 * (parentA.Extinction + parentB.Extinction);
			}

            private void EvaluateFitness(Individual[] entities) {
                for(int i=0; i<entities.Length; i++) {
                    entities[i].Fitness = Func(entities[i].Genes);
                }
            }

            //Sorts all individuals starting with best (lowest) fitness
            private void SortByFitness(Individual[] entities) {
                System.Array.Sort(entities,
                    delegate(Individual a, Individual b) {
                        return a.Fitness.CompareTo(b.Fitness);
                    }
                );
            }

            //Compute extinction values
            private void AssignExtinctions(Individual[] entities) {
                double min = entities[0].Fitness;
                double max = entities[Size-1].Fitness;
                for(int i=0; i<entities.Length; i++) {
                    double grading = (double)i/((double)entities.Length-1.0);
                    entities[i].Extinction = (entities[i].Fitness + min*(grading-1.0)) / max;
                }
            }

            private class Individual {
                public double Fitness;
                public double[] Genes;
                public double[] Momentum;
                public double Extinction;

                public Individual(int dimensionality) {
                    Genes = new double[dimensionality];
                    Momentum = new double[dimensionality];
                }
            }
        }
    }
}
#endif

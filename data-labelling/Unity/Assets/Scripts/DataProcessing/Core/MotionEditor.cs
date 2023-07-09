#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

/*
This is the motion editor used for first submission working with the PFNN
We have since changed the autolabelling to work with the local phase module instead of the phase module#
Oct 2021
*/

[ExecuteInEditMode]
public class MotionEditor : MonoBehaviour {

	public string Folder = string.Empty;
	public string CSV = string.Empty;
	public MotionFile[] Files = new MotionFile[0];

	public bool Mirror = false;
	public bool InspectSettings = false;

	private bool CameraFocus = false;
	private float FocusHeight = 0f;
	private float FocusOffset = 0f;
	private float FocusDistance = 2.5f;
	private float FocusAngle = 90f;
	private float FocusSmoothing = 0.05f;

	private bool Playing = false;
	private float Timescale = 1f;
	private float Timestamp = 0f;
	private float Window = 1f;

	private int tpose_frame_idx = 1;

	private MotionFile Instance = null;
	private Actor Actor = null;

    void Update() {
        //Validation
		bool repaired = false;
        for(int i=0; i<Files.Length; i++) {
            if(Files[i] == null) {
            	Debug.Log("Removing missing file.");
                ArrayExtensions.RemoveAt(ref Files, i);
                i--;
				repaired = true;
            } else if(Files[i].Data == null) {
				Debug.Log("Removing file " + Files[i].name + " because no data could be found.");
				Utility.Destroy(Files[i].gameObject);
				ArrayExtensions.RemoveAt(ref Files, i);
				i--;
				repaired = true;
			}
        }
		if(repaired) {
			for(int i=0; i<Files.Length; i++) {
				Files[i].SetActive(Files[i] == Instance);
				Files[i].SetIndex(i);
			}
		}
		//
    }

	public void SetMirror(bool value) {
		if(Mirror != value) {
			Mirror = value;
			LoadFrame(Timestamp);
		}
	}

	public void SetScaling(float value) {
		if(GetCurrentFile().Data.Scaling != value) {
			GetCurrentFile().Data.Scaling = value;
			LoadFrame(Timestamp);
		}
	}

	public void SetCameraFocus(bool value) {
		if(CameraFocus != value) {
			CameraFocus = value;
			if(!CameraFocus) {
				Vector3 position =  SceneView.lastActiveSceneView.camera.transform.position;
				Quaternion rotation = Quaternion.Euler(0f, SceneView.lastActiveSceneView.camera.transform.rotation.eulerAngles.y, 0f);
				SceneView.lastActiveSceneView.LookAtDirect(position, rotation, 0f);
			}
		}
	}

	public float GetWindow() {
		return GetCurrentFile() == null ? 0f : Window * GetCurrentFile().Data.GetTotalTime();
	}

	public Actor GetActor() {
		if(Actor == null) {
			Actor = GetComponentInChildren<Actor>();
		}
		if(Actor == null) {
			Actor = CreateSkeleton();
		}
		return Actor;
	}

	public MotionFile[] GetFiles() {
		return Files;
	}

	public MotionFile GetCurrentFile() {
		return Instance;
	}

	public MotionScene GetCurrentScene() {
		return GetCurrentFile() == null ? null : GetCurrentFile().GetActiveScene();
	}

	public Frame GetCurrentFrame() {
		return GetCurrentFile() == null ? null : GetCurrentFile().Data.GetFrame(Timestamp);
	}

	public void Import() {
		//Import data and destroy dead objects
		string[] assets = AssetDatabase.FindAssets("t:MotionData", new string[1]{Folder});
		List<MotionFile> files = new List<MotionFile>();
		for(int i=0; i<assets.Length; i++) {
			MotionFile file = null;
			MotionData data = (MotionData)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(assets[i]), typeof(MotionData));
			foreach(MotionFile instance in GetComponentsInChildren<MotionFile>(true)) {
				if(instance.Data == data) {
					file = instance;
					break;
				}
			}
			if(file == null) {
				file = new GameObject(data.name).AddComponent<MotionFile>();
				file.transform.SetParent(transform);
				file.Data = data;
				file.AddScene();
			}
			files.Add(file);
		}
		foreach(MotionFile instance in GetComponentsInChildren<MotionFile>(true)) {
			if(!files.Contains(instance)) {
				Utility.Destroy(instance.gameObject);
			}
		}
		for(int i=0; i<files.Count; i++) {
			files[i].SetIndex(i);
		}
		//Assign files
		Files = files.ToArray();
		//Load first file
		Instance = null;
		if(Files.Length > 0) {
			LoadFile(Files[0]);
		}
	}

	public void AutoProcess(){
		StartCoroutine(AutoProcessor());
	}

	private IEnumerator AutoProcessor() {
		// AutoProcess automatically adds the trajectory module, style and gait labels, phase label and does mirroring based on if it is symmetric or not.
		// The output shouls be manually checked however, as there is high possibility of errors

		// This is a hack but in order for the coroutines to work simply play the application and then
		// run autoprocess. If you don't do this the code gets stuck waiting for x seconds

		// CSV /home/ian/AI4AnimationStyle/Data_Information/Dataset_List.csv
		string[] lines = File.ReadAllLines(CSV);
		List<string> styleNames = new List<string>();
		bool firstLine = true;
		foreach(string line in lines){
			if(!firstLine){
				styleNames.Add(line.Split(',')[0]);
			}
			firstLine = false;			
		}
		// How we can iterate over each of the files
		for(int i =0; i<Files.Length; i++){
			LoadFile(Files[i]);  // Loads file i into Instance
			string current_file_name = Instance.Data.name;
			Instance.Data.AddModule((Module.TYPE)(0)); // 0 is Trajectory
			Instance.Data.AddModule((Module.TYPE)(2)); // 2 is Phase
			Instance.Data.AddModule((Module.TYPE)(3)); // 3 is  Gait
			Instance.Data.AddModule((Module.TYPE)(1)); // 1 is Style			
			TrajectoryModule TrajMod = (TrajectoryModule)Instance.Data.GetModule((Module.TYPE)(0));
			StyleModule StyleMod = (StyleModule)Instance.Data.GetModule((Module.TYPE)(1));
			PhaseModule PhaseMod = (PhaseModule)Instance.Data.GetModule((Module.TYPE)(2));
			GaitModule GaitMod = (GaitModule)Instance.Data.GetModule((Module.TYPE)(3));
			

			// Auto trajectory
			TrajMod.ForwardAxis = MotionData.AXIS.ZPositive;

			// Auto style label
			foreach(string styleName in styleNames){
				StyleMod.AddStyle(styleName);
			}
			string current_style_name = current_file_name.Split('_')[0];
			StyleMod.Functions[styleNames.FindIndex(x => x == current_style_name)].Toggle(Instance.Data.GetFrame(1)); // frame 1
			StyleMod.Functions[styleNames.FindIndex(x => x == current_style_name)].Toggle(Instance.Data.GetFrame(Instance.Data.GetTotalFrames())); // last frame

			// Auto gait label
			string gait_name = current_file_name.Split('_')[1].Split('.')[0];
			GaitMod.AddGait("Walk");
			GaitMod.AddGait("Run");
			GaitMod.AddGait("Idle");
			if(gait_name == "FW" || gait_name == "BW" || gait_name == "SW"){
				GaitMod.Functions[0].Toggle(Instance.Data.GetFrame(1)); // frame 1
				GaitMod.Functions[0].Toggle(Instance.Data.GetFrame(Instance.Data.GetTotalFrames())); 
			}
			else if(gait_name == "FR" || gait_name == "BR" || gait_name == "SR"){
				GaitMod.Functions[1].Toggle(Instance.Data.GetFrame(1)); // frame 1
				GaitMod.Functions[1].Toggle(Instance.Data.GetFrame(Instance.Data.GetTotalFrames())); 
			}
			else if(gait_name == "ID"){
				GaitMod.Functions[2].Toggle(Instance.Data.GetFrame(1)); // frame 1
				GaitMod.Functions[2].Toggle(Instance.Data.GetFrame(Instance.Data.GetTotalFrames())); 
			}


			// Auto phase label
			PhaseMod.ToggleVariable(21); // 21 is Right Toe for Xsens skeletal data
			yield return new WaitForSeconds(0f);
			EditorCoroutines.StartCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
			EditorCoroutines.StartCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
			// Debug.Log("Optimising phase for file " + i.ToString());
			yield return new WaitForSeconds(15f);
			EditorCoroutines.StopCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
			EditorCoroutines.StopCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
			yield return new WaitForSeconds(0f);

			//Auto settings
			Instance.Data.MirrorAxis = MotionData.AXIS.XPositive; // Set mirror axis
			Instance.Data.Export = true; // set export flag to true
		}

	}

	public void AddJointPhases(){
		StartCoroutine(AddJointPhasesCoroutine());
	}

	private IEnumerator AddJointPhasesCoroutine() {
		// AddJointPhases adds phase labels for every remaing joint (the right toe is already done in autoproces)

		// Again have to play the application in order for the coroutine to work
		for(int i =0; i<Files.Length; i++){
			LoadFile(Files[i]);  // Loads file i into Instance
			string current_file_name = Instance.Data.name;
			PhaseModule PhaseMod = (PhaseModule)Instance.Data.GetModule((Module.TYPE)(2));
			for(int j = 0; j<PhaseMod.Variables.Length-1; j++){
				Instance.Data.AddExtraPhaseModule(); // 2 is Phase	
			}	
			for(int j = 0; j<21; j++){
				PhaseMod = (PhaseModule)Instance.Data.GetModules((Module.TYPE)(2))[j+1];
				PhaseMod.ToggleVariable(j);
				yield return new WaitForSeconds(0f);
				EditorCoroutines.StartCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
				EditorCoroutines.StartCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
				yield return new WaitForSeconds(15f);
				EditorCoroutines.StopCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
				EditorCoroutines.StopCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
				yield return new WaitForSeconds(0f);
			}
			for(int j = 22; j<PhaseMod.Variables.Length; j++){
				PhaseMod = (PhaseModule)Instance.Data.GetModules((Module.TYPE)(2))[j];
				PhaseMod.ToggleVariable(j);
				yield return new WaitForSeconds(0f);
				EditorCoroutines.StartCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
				EditorCoroutines.StartCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
				yield return new WaitForSeconds(15f);
				EditorCoroutines.StopCoroutine(PhaseMod.RegularPhaseFunction.Optimise(), PhaseMod);
				EditorCoroutines.StopCoroutine(PhaseMod.InversePhaseFunction.Optimise(), PhaseMod);
				yield return new WaitForSeconds(0f);	
			}	
		}
	}

	public void AddFootContacts() {
		// How we can iterate over each of the files
		for(int i =0; i<Files.Length; i++){
			LoadFile(Files[i]);  // Loads file i into Instance
			string current_file_name = Instance.Data.name;
			Instance.Data.AddModule((Module.TYPE)(4)); // 4 is Contact			
			ContactModule ContactMod = (ContactModule)Instance.Data.GetModule((Module.TYPE)(4));
		}

	}

	public async void AddLocalPhases(){
		int file_fit = 0; //
		for(int i =0; i<Files.Length; i++){
			LoadFile(Files[i]);  // Loads file i into Instance
			string current_file_name = Instance.Data.name;
			Instance.Data.AddModule((Module.TYPE)(5)); // 5 is local phase
			LocalPhaseModule LocalPhaseMod = (LocalPhaseModule)Instance.Data.GetModule((Module.TYPE)(5));
			LocalPhaseMod.StartFitting();
			while (LocalPhaseMod.IsFitting()){
				// Do not debug in this loop - seems to cause Unity to crash
				// https://answers.unity.com/questions/1298302/is-it-just-me-or-does-debug-log-crash-unity-on-lar.html
				// Debug.Log("Fitting in progress. File: " + file_fit.ToString()); 
				await Task.Delay(10);
			}
			LocalPhaseMod.StopFitting();
			// Debug.Log("Fitting stopped.");
			file_fit += 1;
		}
	}

	public void AugmentData(){
		// Look for a sensible sampling distn
		// Which may involve looking at SMPL anyway
		// 3. Joint angles and working with SMPL
		float tstep = 1/GetCurrentFile().Data.Framerate;
		Frame tpose_frame = GetCurrentFile().Data.GetFrame(tpose_frame_idx);
		for(int t=0; t<200; t++){
			// Frame frame = GetCurrentFrame();
			Frame frame = GetCurrentFile().Data.GetFrame(Timestamp + t*tstep);
			// GetActor().transform.position = GetActor().Bones.Length == 0 ? Vector3.zero : frame.GetBoneTransformation(0, Mirror).GetPosition();
			// GetActor().transform.rotation = GetActor().Bones.Length == 0 ? Quaternion.identity : frame.GetBoneTransformation(0, Mirror).GetRotation();
			Matrix4x4[] posture = frame.GetBoneTransformations(false);
			// Vector3[] velocities = frame.GetBoneVelocities(Mirror);
			// Trajectory trajectory = ((TrajectoryModule)GetCurrentFile().Data.GetModule(Module.TYPE.Trajectory)).GetTrajectory(frame, Mirror);
			// Debug.Log(trajectory.Points[1].GetPosition());
			
			float scl = 1.2f;
			for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++) {
				// Works in world space, this is correct, taller people should move further, also means the feet do not cut through the floor.
				// Don't need to scale velocities as scaling positions does it automatically
				// Similarly We don't need to manually alter the trajectory, it is derived from the root transformations in the frames
				// When we update the data to store the new transformations the trajectory will update too
				Matrix4x4Extensions.SetPosition(ref frame.World[i], frame.World[i].GetPosition()*scl);
				// Debug.Log(velocities[i]);			
			}			
			Dictionary<int, List<int>> ForwardsHierachy = new Dictionary<int, List<int>>();
			Dictionary<int, List<int>> BackwardsHierachy = new Dictionary<int, List<int>>();
			Dictionary<int, List<Vector3>> Fwd_Local_Positions = new Dictionary<int, List<Vector3>>();
			for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++){
				ForwardsHierachy.Add(i, new List<int>());
				BackwardsHierachy.Add(i, new List<int>());
				Fwd_Local_Positions.Add(i, new List<Vector3>());
			}
			// Forwards hierachy updates bones world positions from the root upwards after stretching/shrinking a given boon
			// Chest                                      0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[1].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});		
			// Chest2                                     0,  1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[2].AddRange(new List<int>(){-1, -1, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});		
			// Chest3                                     0,  1,  2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[3].AddRange(new List<int>(){-1, -1, -1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});		
			// Chest4                                     0,  1,  2,  3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[4].AddRange(new List<int>(){-1, -1, -1, -1, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// Neck                                       0,  1,  2,  3,  4, 5, 6, 7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[5].AddRange(new List<int>(){-1, -1, -1, -1, -1, 4, 5, 6, -1, -1, -1, -1, -1,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// Head                                       0,  1,  2,  3,  4,  5, 6, 7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[6].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, 5, 6, -1, -1, -1, -1, -1,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// Head Site                                  0,  1,  2,  3,  4,  5,  6, 7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[7].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, 6, -1, -1, -1, -1, -1,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// RightCollar                                0,  1,  2,  3,  4,  5,  6,  7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[8].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, 4, 8,  9, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// RightCollar                                0,  1,  2,  3,  4,  5,  6,  7,  8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[9].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, 8,  9, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// RightCollar                                 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[10].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1,  9, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// RightCollar                                 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[11].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 10, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// RightCollar                                 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[12].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// LeftCollar                                  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[13].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// LeftCollar                                  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[14].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// LeftCollar                                  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[15].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// LeftCollar                                  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[16].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			// LeftCollar                                  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[17].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			
			// RightHip                                    0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[18].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 18, 19, 20, 21, -1, -1, -1, -1, -1});
			// LeftHip                                     0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			ForwardsHierachy[23].AddRange(new List<int>(){-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 23, 24, 25, 26});

			ForwardsHierachy[19].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[20].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[21].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[22].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[24].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[25].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[26].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			ForwardsHierachy[27].AddRange(new List<int>(){-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1});
			
			// This is not the true parents, rather what we consider to be the parents for the purposes of changing the joint ratios
			//                               0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27
			int[] RatioParents = new int[] {-1, 0, 1, 2, 3, 4, 5, 6, 4, 8,  9, 10, 11,  4, 13, 14, 15, 16,  0, 20, 21, 22, -1,  0, 25, 26, 27, -1};
			
			Dictionary<int, float> BoneRatios = new Dictionary<int, float>();
			for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++){
				BoneRatios.Add(i, 1f);
			}
			// The ratio of hips and shoulders should be loosely paired as often these change together if rigging a larger character
			// Spine & Neck Bones
			// BoneRatios[1]=1f;
			// BoneRatios[2]=1f;
			// BoneRatios[3]=1f;
			// BoneRatios[4]=1f;
			// BoneRatios[5]=1f;
			// BoneRatios[6]=1f;
			// BoneRatios[7]=1f;
			// // Paired arm bones
			// BoneRatios[8]=1f;   BoneRatios[13]=1f; 
			// BoneRatios[9]=1f;   BoneRatios[14]=1f;
			// BoneRatios[10]=0.8f;  BoneRatios[15]=0.8f;
			// BoneRatios[11]=0.8f;  BoneRatios[16]=0.8f;
			// BoneRatios[12]=1f;  BoneRatios[17]=1f;
			// // Hips - wider or narrower
			// BoneRatios[18]=1.5f;  BoneRatios[23]=1.5f;
			// // Paired leg bones
			// BoneRatios[19]=0.7f;  BoneRatios[24]=0.7f;
			// BoneRatios[20]=1f;  BoneRatios[25]=1f;
			// // Paired foot bones
			// BoneRatios[21]=0.7f;  BoneRatios[26]=0.7f;
			// BoneRatios[22]=0.7f;  BoneRatios[27]=0.7f;  // small bone at end of foot

			// BoneRatios[1]=1f;
			// BoneRatios[2]=1f;
			// BoneRatios[3]=1.3f;
			// BoneRatios[4]=1f;
			// BoneRatios[5]=1f;
			// BoneRatios[6]=1f;
			// BoneRatios[7]=1f;
			// // Paired arm bones
			// BoneRatios[8]=0.7f;   BoneRatios[13]=0.7f; 
			// BoneRatios[9]=1f;   BoneRatios[14]=1f;
			// BoneRatios[10]=1.1f;  BoneRatios[15]=1.1f;
			// BoneRatios[11]=0.8f;  BoneRatios[16]=0.8f;
			// BoneRatios[12]=1f;  BoneRatios[17]=1f;
			// // Hips - wider or narrower
			// BoneRatios[18]=0.6f;  BoneRatios[23]=0.6f;
			// // Paired leg bones
			// BoneRatios[19]=0.8f;  BoneRatios[24]=0.8f;
			// BoneRatios[20]=0.7f;  BoneRatios[25]=0.7f;
			// // Paired foot bones
			// BoneRatios[21]=1.2f;  BoneRatios[26]=1.2f;
			// BoneRatios[22]=0.8f;  BoneRatios[27]=0.8f;  // small bone at end of foot

			// BoneRatios[1]=1f;
			// BoneRatios[2]=1.1f;
			// BoneRatios[3]=1f;
			// BoneRatios[4]=1f;
			// BoneRatios[5]=1f;
			// BoneRatios[6]=0.7f;
			// BoneRatios[7]=1f;
			// // Paired arm bones
			// BoneRatios[8]=1f;   BoneRatios[13]=1f; 
			// BoneRatios[9]=1f;   BoneRatios[14]=1f;
			// BoneRatios[10]=1f;  BoneRatios[15]=1f;
			// BoneRatios[11]=1f;  BoneRatios[16]=1f;
			// BoneRatios[12]=1f;  BoneRatios[17]=1f;
			// // Hips - wider or narrower
			// BoneRatios[18]=1f;  BoneRatios[23]=1f;
			// // Paired leg bones
			// BoneRatios[19]=1.4f;  BoneRatios[24]=1.4f;
			// BoneRatios[20]=1.3f;  BoneRatios[25]=1.3f;
			// // Paired foot bones
			// BoneRatios[21]=0.7f;  BoneRatios[26]=0.7f;
			// BoneRatios[22]=0.6f;  BoneRatios[27]=0.6f;  // small bone at end of foot

			BoneRatios[1]=1.3f;
			BoneRatios[2]=1f;
			BoneRatios[3]=1.1f;
			BoneRatios[4]=1f;
			BoneRatios[5]=1f;
			BoneRatios[6]=1f;
			BoneRatios[7]=1.2f;
			// Paired arm bones
			BoneRatios[8]=1f;   BoneRatios[13]=1f; 
			BoneRatios[9]=1f;   BoneRatios[14]=1f;
			BoneRatios[10]=1.2f;  BoneRatios[15]=1.2f;
			BoneRatios[11]=1f;  BoneRatios[16]=1f;
			BoneRatios[12]=1.2f;  BoneRatios[17]=1.2f;
			// Hips - wider or narrower
			BoneRatios[18]=1.3f;  BoneRatios[23]=1.3f;
			// Paired leg bones
			BoneRatios[19]=0.6f;  BoneRatios[24]=0.6f;
			BoneRatios[20]=1.1f;  BoneRatios[25]=1.1f;
			// Paired foot bones
			BoneRatios[21]=1.2f;  BoneRatios[26]=1.2f;
			BoneRatios[22]=1.2f;  BoneRatios[27]=1.2f;  // small bone at end of foot

			// Store initial local positions for upper body
			foreach(KeyValuePair<int,float> entry in BoneRatios){
				if(entry.Key == 0){  
					continue; // root
				}
				if(entry.Key == 19 || entry.Key == 20 || entry.Key == 21 || entry.Key == 22 ||
				   entry.Key == 24 || entry.Key == 25 || entry.Key == 26 || entry.Key == 27){
					continue; // lower body
				}
				// Store current local positions		
				// List<Vector3> fwd_local_positions = new List<Vector3>();
				for(int i=0; i<GetActor().Bones.Length; i++){
					if(ForwardsHierachy[entry.Key][i]==-1){
						Fwd_Local_Positions[entry.Key].Add(Vector3.zero);
					}
					else{
						Fwd_Local_Positions[entry.Key].Add(frame.World[i].GetRelativeTransformationTo(frame.World[ForwardsHierachy[entry.Key][i]]).GetPosition());
					}			
				}
			}

			// lower body
			Matrix4x4 init_root = frame.World[0];
			Matrix4x4 init_rhip = frame.World[18];
			Matrix4x4 init_lhip = frame.World[23];
			
			Matrix4x4 init_rank = frame.World[20];
			Matrix4x4 init_lank = frame.World[25];
			Matrix4x4 init_rtoe = frame.World[21];
			Matrix4x4 init_ltoe = frame.World[26];
			Matrix4x4 init_rfoot = frame.World[22];
			Matrix4x4 init_lfoot = frame.World[27];

			// Store forwards local positions
			List<Vector3> rleg_fwd_local_positions = new List<Vector3>();
			rleg_fwd_local_positions.Add(frame.World[19].GetRelativeTransformationTo(frame.World[18]).GetPosition());
			rleg_fwd_local_positions.Add(frame.World[20].GetRelativeTransformationTo(frame.World[19]).GetPosition());
			rleg_fwd_local_positions.Add(frame.World[21].GetRelativeTransformationTo(frame.World[20]).GetPosition());
			rleg_fwd_local_positions.Add(frame.World[22].GetRelativeTransformationTo(frame.World[21]).GetPosition());
			List<Vector3> lleg_fwd_local_positions = new List<Vector3>();
			lleg_fwd_local_positions.Add(frame.World[24].GetRelativeTransformationTo(frame.World[23]).GetPosition());
			lleg_fwd_local_positions.Add(frame.World[25].GetRelativeTransformationTo(frame.World[24]).GetPosition());
			lleg_fwd_local_positions.Add(frame.World[26].GetRelativeTransformationTo(frame.World[25]).GetPosition());
			lleg_fwd_local_positions.Add(frame.World[27].GetRelativeTransformationTo(frame.World[26]).GetPosition());
			// Store backwards local positions
			List<Vector3> rleg_bwd_local_positions = new List<Vector3>();
			rleg_bwd_local_positions.Add(frame.World[18].GetRelativeTransformationTo(frame.World[19]).GetPosition());
			rleg_bwd_local_positions.Add(frame.World[19].GetRelativeTransformationTo(frame.World[20]).GetPosition());
			rleg_bwd_local_positions.Add(frame.World[20].GetRelativeTransformationTo(frame.World[21]).GetPosition());
			rleg_bwd_local_positions.Add(frame.World[21].GetRelativeTransformationTo(frame.World[22]).GetPosition());
			List<Vector3> lleg_bwd_local_positions = new List<Vector3>();
			lleg_bwd_local_positions.Add(frame.World[23].GetRelativeTransformationTo(frame.World[24]).GetPosition());
			lleg_bwd_local_positions.Add(frame.World[24].GetRelativeTransformationTo(frame.World[25]).GetPosition());
			lleg_bwd_local_positions.Add(frame.World[25].GetRelativeTransformationTo(frame.World[26]).GetPosition());
			lleg_bwd_local_positions.Add(frame.World[26].GetRelativeTransformationTo(frame.World[27]).GetPosition());

			float init_rleglen = 0;
			float init_lleglen = 0;
			float new_rleglen = 0;
			float new_lleglen = 0;
			float init_rfootlen = 0;
			float init_lfootlen = 0;
			float new_rfootlen = 0;
			float new_lfootlen = 0;

			for(int i=18; i<20; i++){  // only thigh and calf
				init_rleglen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude;
			}
			// Add contribution of ankle height to overall height
			Matrix4x4 rank_proj = tpose_frame.World[20];
			Matrix4x4Extensions.SetPosition(ref rank_proj, new Vector3(tpose_frame.World[20].GetPosition().x, tpose_frame.World[21].GetPosition().y, tpose_frame.World[20].GetPosition().z));
			float rlegang = Vector3.Angle(tpose_frame.World[21].GetRelativeTransformationTo(rank_proj).GetPosition(),
										  tpose_frame.World[21].GetRelativeTransformationTo(tpose_frame.World[20]).GetPosition());
			init_rleglen += (tpose_frame.World[20].GetPosition() - tpose_frame.World[21].GetPosition()).magnitude * Mathf.Sin(Mathf.Deg2Rad * rlegang);
			Debug.Log("R Leg Angle");
			Debug.Log(rlegang);
			// Add contribution of toe height to overall height
			Matrix4x4 rtoe_proj = tpose_frame.World[21];
			Matrix4x4Extensions.SetPosition(ref rtoe_proj, new Vector3(tpose_frame.World[21].GetPosition().x, tpose_frame.World[22].GetPosition().y, tpose_frame.World[21].GetPosition().z));
			float rtoeang = Vector3.Angle(tpose_frame.World[22].GetRelativeTransformationTo(rtoe_proj).GetPosition(),
										  tpose_frame.World[22].GetRelativeTransformationTo(tpose_frame.World[21]).GetPosition());
			init_rleglen += (tpose_frame.World[21].GetPosition() - tpose_frame.World[22].GetPosition()).magnitude * Mathf.Sin(Mathf.Deg2Rad * rtoeang);
			Debug.Log("R Toe Angle");
			Debug.Log(rtoeang);
			for(int i=20; i<22; i++){  // only foot bones
				init_rfootlen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude;
			}


			for(int i=23; i<25; i++){
				init_lleglen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude;
			}
			Matrix4x4 lank_proj = tpose_frame.World[25];
			Matrix4x4Extensions.SetPosition(ref lank_proj, new Vector3(tpose_frame.World[25].GetPosition().x, tpose_frame.World[26].GetPosition().y, tpose_frame.World[25].GetPosition().z));
			float llegang = Vector3.Angle(tpose_frame.World[26].GetRelativeTransformationTo(lank_proj).GetPosition(),
										  tpose_frame.World[26].GetRelativeTransformationTo(tpose_frame.World[25]).GetPosition());
			init_lleglen += (tpose_frame.World[25].GetPosition() - tpose_frame.World[26].GetPosition()).magnitude * Mathf.Sin(Mathf.Deg2Rad * llegang);
			Debug.Log("L Angle");
			Debug.Log(llegang);
			Matrix4x4 ltoe_proj = tpose_frame.World[26];
			Matrix4x4Extensions.SetPosition(ref ltoe_proj, new Vector3(tpose_frame.World[26].GetPosition().x, tpose_frame.World[27].GetPosition().y, tpose_frame.World[26].GetPosition().z));
			float ltoeang = Vector3.Angle(tpose_frame.World[27].GetRelativeTransformationTo(ltoe_proj).GetPosition(),
										  tpose_frame.World[27].GetRelativeTransformationTo(tpose_frame.World[26]).GetPosition());
			init_lleglen += (tpose_frame.World[26].GetPosition() - tpose_frame.World[27].GetPosition()).magnitude * Mathf.Sin(Mathf.Deg2Rad * ltoeang);
			Debug.Log("L Toe Angle");
			Debug.Log(ltoeang);
			for(int i=25; i<27; i++){
				init_lfootlen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude;
			}

			for(int i=18; i<20; i++){ 
				new_rleglen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude * BoneRatios[i+1];	
			}
			new_rleglen += (tpose_frame.World[20].GetPosition() - tpose_frame.World[21].GetPosition()).magnitude * BoneRatios[21] * Mathf.Sin(Mathf.Deg2Rad * rlegang);
			new_rleglen += (tpose_frame.World[21].GetPosition() - tpose_frame.World[22].GetPosition()).magnitude * BoneRatios[22] * Mathf.Sin(Mathf.Deg2Rad * rtoeang);
			for(int i=20; i<22; i++){ 
				new_rfootlen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude * BoneRatios[i+1];	
			}

			for(int i=23; i<25; i++){
				new_lleglen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude * BoneRatios[i+1];			
			}
			new_lleglen += (tpose_frame.World[25].GetPosition() - tpose_frame.World[26].GetPosition()).magnitude * BoneRatios[26] * Mathf.Sin(Mathf.Deg2Rad * llegang);
			new_lleglen += (tpose_frame.World[26].GetPosition() - tpose_frame.World[27].GetPosition()).magnitude * BoneRatios[27] * Mathf.Sin(Mathf.Deg2Rad * ltoeang);
			for(int i=25; i<27; i++){
				new_lfootlen += (tpose_frame.World[i].GetPosition() - tpose_frame.World[i+1].GetPosition()).magnitude * BoneRatios[i+1];			
			}
						
			float scale_ratio = (new_lleglen / init_lleglen + new_rleglen / init_lleglen)/2f;
			float foot_ratio = (new_lfootlen / init_lfootlen + new_rfootlen / init_lfootlen)/2f;
			Debug.Log(scale_ratio);
			Debug.Log(foot_ratio);
			// Vector3 new_root_pos = new Vector3(init_root.GetPosition().x, init_root.GetPosition().y * scale_ratio, init_root.GetPosition().z);
			// Vector3 new_rhip_pos = new Vector3(init_rhip.GetPosition().x, init_rhip.GetPosition().y * scale_ratio, init_rhip.GetPosition().z);
			// Vector3 new_lhip_pos = new Vector3(init_lhip.GetPosition().x, init_lhip.GetPosition().y * scale_ratio, init_lhip.GetPosition().z);
			Vector3 new_root_pos = init_root.GetPosition()*scale_ratio;
			Vector3 new_rhip_pos = init_rhip.GetPosition()*scale_ratio;
			Vector3 new_lhip_pos = init_lhip.GetPosition()*scale_ratio;
			// Debug.Log(frame.World[18].GetPosition());
			// Initialise IK
			Matrix4x4Extensions.SetPosition(ref frame.World[0], new_root_pos);
			Matrix4x4Extensions.SetPosition(ref frame.World[18], new_rhip_pos);
			Matrix4x4Extensions.SetPosition(ref frame.World[23], new_lhip_pos);

			Matrix4x4Extensions.SetPosition(ref frame.World[19], (rleg_fwd_local_positions[0]*BoneRatios[19]).GetRelativePositionFrom(frame.World[18]));
			Matrix4x4Extensions.SetPosition(ref frame.World[20], (rleg_fwd_local_positions[1]*BoneRatios[20]).GetRelativePositionFrom(frame.World[19]));
			Matrix4x4Extensions.SetPosition(ref frame.World[21], (rleg_fwd_local_positions[2]*BoneRatios[21]).GetRelativePositionFrom(frame.World[20]));
			Matrix4x4Extensions.SetPosition(ref frame.World[22], (rleg_fwd_local_positions[3]*BoneRatios[22]).GetRelativePositionFrom(frame.World[21]));

			Matrix4x4Extensions.SetPosition(ref frame.World[24], (rleg_fwd_local_positions[0]*BoneRatios[24]).GetRelativePositionFrom(frame.World[23]));
			Matrix4x4Extensions.SetPosition(ref frame.World[25], (rleg_fwd_local_positions[1]*BoneRatios[25]).GetRelativePositionFrom(frame.World[24]));
			Matrix4x4Extensions.SetPosition(ref frame.World[26], (rleg_fwd_local_positions[2]*BoneRatios[26]).GetRelativePositionFrom(frame.World[25]));
			Matrix4x4Extensions.SetPosition(ref frame.World[27], (rleg_fwd_local_positions[3]*BoneRatios[27]).GetRelativePositionFrom(frame.World[26]));

			// IK Retargeting
			// This is a bit ratchet, the IK solver works on Transforms in a skeleton, so we set the actor positions here
			// then use this to reset the world positions (which are in turn used to set actor pose when LoadFrame is called)
			// Initialise, match hip location & scale. Is initialised as previous pose.
			// We also set Mirror to false, if true it doesn't work
			GetActor().transform.position = GetActor().Bones.Length == 0 ? Vector3.zero : frame.GetBoneTransformation(0, false).GetPosition();
			GetActor().transform.rotation = GetActor().Bones.Length == 0 ? Quaternion.identity : frame.GetBoneTransformation(0, false).GetRotation();
			posture = frame.GetBoneTransformations(false);
			// Vector3[] velocities = frame.GetBoneVelocities(Mirror);
			// Sets bone positions using the World positions calculated above
			for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++) {
				GetActor().Bones[i].Transform.position = posture[i].GetPosition();
				GetActor().Bones[i].Transform.rotation = posture[i].GetRotation();
				// GetActor().Bones[i].Velocity = velocities[i];
			}

			// Solving for thigh and calf by fixing position of feet and toes
			// Effectively the only thing that can change is the angle of the knee!
			// Some ideas from http://graphics.snu.ac.kr/~kjchoi/publication/omr.pdf 
			UltimateIK.Model IKModel;
			Transform[] joints = new Transform[]{GetActor().Bones[18].Transform, GetActor().Bones[23].Transform, GetActor().Bones[20].Transform, GetActor().Bones[25].Transform, 
												GetActor().Bones[21].Transform, GetActor().Bones[26].Transform, GetActor().Bones[22].Transform,	GetActor().Bones[27].Transform};
			// Transform[] joints = new Transform[]{GetActor().Bones[22].Transform, GetActor().Bones[27].Transform};			
			IKModel = UltimateIK.BuildModel(GetActor().Bones[0].Transform, joints);
			IKModel.SetIterations(25);

			// Sets global ankle position - most notably height
			// Matrix4x4Extensions.SetPosition(ref init_rank, init_rank.GetPosition()*scale_ratio);
			// Matrix4x4Extensions.SetPosition(ref init_lank, init_lank.GetPosition()*scale_ratio);
			
			// Matrix4x4Extensions.SetPosition(ref init_rtoe, (rleg_fwd_local_positions[2]*BoneRatios[21]).GetRelativePositionFrom(init_rank));
			// Matrix4x4Extensions.SetPosition(ref init_ltoe, (lleg_fwd_local_positions[2]*BoneRatios[26]).GetRelativePositionFrom(init_lank));

			// Matrix4x4Extensions.SetPosition(ref init_rfoot, (rleg_fwd_local_positions[3]*BoneRatios[22]).GetRelativePositionFrom(init_rtoe));
			// Matrix4x4Extensions.SetPosition(ref init_lfoot, (lleg_fwd_local_positions[3]*BoneRatios[27]).GetRelativePositionFrom(init_ltoe));	

			// Note: the above is not totally correct. If we were not doing the global scaling as well then what we should do
			// is take the ankle height and scale by foot_bone*sin(a)+toe_bone*sin(b) where a and b are the corresponding angles
			// between the bone and a plane parallel to the floor crossing through the point.
			// But with the global scaling as well it gets kind of tricky to undo, and this is pretty close so we can use it for now
			// and try and improve later. There are some small floats/cut through and a tiny bit of sliding.

			// The below is the best we have but does not avoid all sliding particularly for very odd skeletal shapes
			// Maybe there is a deterministic calculation but I am struggling to see it
			// Currently I think if you wish to remove more sliding you may have to use the approx foot contact detection
			// and if it is on, fix the foot pos. As we do this in the demo anyway it's not totally necessary here.	
			Matrix4x4Extensions.SetPosition(ref init_rank, new Vector3(init_rank.GetPosition().x*scale_ratio, init_rank.GetPosition().y*foot_ratio, init_rank.GetPosition().z*scale_ratio));
			Matrix4x4Extensions.SetPosition(ref init_lank, new Vector3(init_lank.GetPosition().x*scale_ratio, init_lank.GetPosition().y*foot_ratio, init_lank.GetPosition().z*scale_ratio));
			
			Matrix4x4Extensions.SetPosition(ref init_rtoe, (rleg_fwd_local_positions[2]*BoneRatios[21]).GetRelativePositionFrom(init_rank));
			Matrix4x4Extensions.SetPosition(ref init_ltoe, (lleg_fwd_local_positions[2]*BoneRatios[26]).GetRelativePositionFrom(init_lank));

			Matrix4x4Extensions.SetPosition(ref init_rfoot, (rleg_fwd_local_positions[3]*BoneRatios[22]).GetRelativePositionFrom(init_rtoe));
			Matrix4x4Extensions.SetPosition(ref init_lfoot, (lleg_fwd_local_positions[3]*BoneRatios[27]).GetRelativePositionFrom(init_ltoe));	

			// Hips
			IKModel.Objectives[0].SetTarget(new_rhip_pos);
			IKModel.Objectives[1].SetTarget(new_lhip_pos);
			// Ankle			
			IKModel.Objectives[2].SetTarget(init_rank.GetPosition());
			IKModel.Objectives[3].SetTarget(init_lank.GetPosition());
			// This scaling moves global position of feet, we don't want this
			// IKModel.Objectives[2].SetTarget(init_rank.GetPosition()*scale_ratio);
			// IKModel.Objectives[3].SetTarget(init_lank.GetPosition()*scale_ratio);
			// Toe
			IKModel.Objectives[4].SetTarget(init_rtoe.GetPosition());
			IKModel.Objectives[5].SetTarget(init_ltoe.GetPosition());			
			// IKModel.Objectives[4].SetTarget(init_rtoe.GetPosition()*scale_ratio);
			// IKModel.Objectives[5].SetTarget(init_ltoe.GetPosition()*scale_ratio);
			// ToeSite			
			// IKModel.Objectives[6].SetTarget(init_rfoot.GetPosition()*scale_ratio);
			// IKModel.Objectives[7].SetTarget(init_lfoot.GetPosition()*scale_ratio);
			IKModel.Objectives[6].SetTarget(init_rfoot.GetPosition());
			IKModel.Objectives[7].SetTarget(init_lfoot.GetPosition());

			float[] IKWeights = new float[]{1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f};
			IKModel.SetWeights(IKWeights);
			IKModel.Solve();

			// Update World matrices with IK solution
			Matrix4x4Extensions.SetPosition(ref frame.World[18], GetActor().Bones[18].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[23], GetActor().Bones[23].Transform.position);

			Matrix4x4Extensions.SetPosition(ref frame.World[19], GetActor().Bones[19].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[20], GetActor().Bones[20].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[21], GetActor().Bones[21].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[22], GetActor().Bones[22].Transform.position);

			Matrix4x4Extensions.SetPosition(ref frame.World[24], GetActor().Bones[24].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[25], GetActor().Bones[25].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[26], GetActor().Bones[26].Transform.position);
			Matrix4x4Extensions.SetPosition(ref frame.World[27], GetActor().Bones[27].Transform.position);
			

			// Update upper body positions after updating lower body with IK
			for(int i=0; i<GetActor().Bones.Length; i++){
				if(i == 0){  
					continue; // root
				}
				if(i == 18 || i == 19 || i == 20 || i == 21 || i == 22 ||
				   i == 23 || i == 24 || i == 25 || i == 26 || i == 27){
					continue; // lower body
				}
				for(int j=0; j<GetActor().Bones.Length; j++){
					if(ForwardsHierachy[i][j]==-1){}
					else{						
						Matrix4x4Extensions.SetPosition(ref frame.World[j], Fwd_Local_Positions[i][j].GetRelativePositionFrom(frame.World[ForwardsHierachy[i][j]]));
					}	
				}	
			}

			// Upper body ratio changing
			foreach(KeyValuePair<int,float> entry in BoneRatios){
				if(entry.Key == 0){  
					continue; // root
				}
				if(entry.Key == 19 || entry.Key == 20 || entry.Key == 21 || entry.Key == 22 ||
				   entry.Key == 24 || entry.Key == 25 || entry.Key == 26 || entry.Key == 27){
					continue; // lower body
				}
				// Store current local positions		
				List<Vector3> fwd_local_positions = new List<Vector3>();
				for(int i=0; i<GetActor().Bones.Length; i++){
					if(ForwardsHierachy[entry.Key][i]==-1 || i==entry.Key){
						// We update the entry.Key position according to it's ratio so we don't want to use its local position, so we ignore its parent in the hierachy
						fwd_local_positions.Add(Vector3.zero);
					}
					else{
						fwd_local_positions.Add(frame.World[i].GetRelativeTransformationTo(frame.World[ForwardsHierachy[entry.Key][i]]).GetPosition());
						// local_positions.Add(frame.World[2].GetRelativeTransformationTo(frame.World[1]).GetPosition());
					}			
				}
				// Update with new joint ratio
				// Easiest to hard code as we consider the 'parent' of the bone to be different depending on if it is in the upper or lower part of the body
				Vector3 new_local_pos = frame.World[entry.Key].GetRelativeTransformationTo(frame.World[RatioParents[entry.Key]]).GetPosition()*entry.Value;
				Matrix4x4Extensions.SetPosition(ref frame.World[entry.Key], new_local_pos.GetRelativePositionFrom(frame.World[RatioParents[entry.Key]]));

				// Update attached bones
				for(int i=0; i<GetActor().Bones.Length; i++){
					if(ForwardsHierachy[entry.Key][i]==-1 || i==entry.Key){}
					else{
						Matrix4x4Extensions.SetPosition(ref frame.World[i], fwd_local_positions[i].GetRelativePositionFrom(frame.World[ForwardsHierachy[entry.Key][i]]));
					}			
				}
			}
			Debug.Log(t);
		}

	}

	public void SaveFile(MotionFile file) {
		if(file != null) {
			EditorUtility.SetDirty(file.Data);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		} else {
			Debug.Log("Saving file failed because it was null.");
		}
	}

	public void LoadFile(MotionFile file) {
		if(Instance != file) {
			for(int i=0; i<Files.Length; i++) {
				Files[i].SetActive(false);
			}
			if(Instance != null) {
				SaveFile(Instance);
			}
			Instance = file;
			if(Instance != null) {
				Instance.SetActive(true);
				LoadFrame(0f);
			}
		}
	}

	public void LoadFrame(float timestamp) {
		Timestamp = timestamp;

		if(Mirror) {
			GetCurrentFile().transform.localScale = Vector3.one.GetMirror(GetCurrentFile().Data.GetAxis(GetCurrentFile().Data.MirrorAxis));
		} else {
			GetCurrentFile().transform.localScale = Vector3.one;
		}

		Frame frame = GetCurrentFrame();
		GetActor().transform.position = GetActor().Bones.Length == 0 ? Vector3.zero : frame.GetBoneTransformation(0, Mirror).GetPosition();
		GetActor().transform.rotation = GetActor().Bones.Length == 0 ? Quaternion.identity : frame.GetBoneTransformation(0, Mirror).GetRotation();
		Matrix4x4[] posture = frame.GetBoneTransformations(Mirror);
		Vector3[] velocities = frame.GetBoneVelocities(Mirror);
		for(int i=0; i<Mathf.Min(GetActor().Bones.Length, posture.Length); i++) {
			// if(i==0){ // to show motion with no root transform
			// 	GetActor().Bones[i].Transform.position = Vector3.zero; //new Vector3(0f, posture[i].GetPosition().y, 0f);
			// }
			// else{
			// 	GetActor().Bones[i].Transform.position = posture[i].GetPosition() - posture[0].GetPosition();
			// }
			GetActor().Bones[i].Transform.position = posture[i].GetPosition();
			GetActor().Bones[i].Transform.rotation = posture[i].GetRotation();
			GetActor().Bones[i].Velocity = velocities[i];
			// Debug.Log(velocities[i]);
		}

		foreach(MotionEvent e in GetCurrentScene().GetComponentsInChildren<MotionEvent>(true)) {
			e.Callback(this);
		}

		if(CameraFocus) {
			if(SceneView.lastActiveSceneView != null) {
				Vector3 lastPosition = SceneView.lastActiveSceneView.camera.transform.position;
				Quaternion lastRotation = SceneView.lastActiveSceneView.camera.transform.rotation;
				Vector3 position = Actor.GetRoot().position;
				position.y += FocusHeight;
				Quaternion rotation = Actor.GetRoot().rotation;
				rotation.x = 0f;
				rotation.z = 0f;
				rotation = Quaternion.Euler(0f, Mirror ? Mathf.Repeat(FocusAngle + 0f, 360f) : FocusAngle, 0f) * rotation;
				position += FocusOffset * (rotation * Vector3.right);
				SceneView.lastActiveSceneView.LookAtDirect(Vector3.Lerp(lastPosition, position, 1f-FocusSmoothing), Quaternion.Slerp(lastRotation, rotation, (1f-FocusSmoothing)), FocusDistance*(1f-FocusSmoothing));
			}
		}
	}

	public void LoadFrame(int index) {
		LoadFrame(GetCurrentFile().Data.GetFrame(index).Timestamp);
	}

	public void LoadPreviousFrame() {
		LoadFrame(Mathf.Max(GetCurrentFrame().Index - 1, 1));
	}

	public void LoadNextFrame() {
		LoadFrame(Mathf.Min(GetCurrentFrame().Index + 1, GetCurrentFile().Data.GetTotalFrames()));
	}

	public void PlayAnimation() {
		if(Playing) {
			return;
		}
		Playing = true;
		EditorCoroutines.StartCoroutine(Play(), this);
	}

	public void StopAnimation() {
		if(!Playing) {
			return;
		}
		Playing = false;
		EditorCoroutines.StopCoroutine(Play(), this);
	}

	private IEnumerator Play() {
		System.DateTime timestamp = Utility.GetTimestamp();
		while(GetCurrentFile() != null) {
			Timestamp += Timescale * (float)Utility.GetElapsedTime(timestamp);
			if(Timestamp > GetCurrentFile().Data.GetTotalTime()) {
				Timestamp = Mathf.Repeat(Timestamp, GetCurrentFile().Data.GetTotalTime());
			}
			timestamp = Utility.GetTimestamp();
			LoadFrame(Timestamp);
			yield return new WaitForSeconds(0f);
		}
	}

	public Actor CreateSkeleton() {
		if(GetCurrentFile() == null) {
			return null;
		}
		Actor actor = new GameObject("Skeleton").AddComponent<Actor>();
		actor.transform.SetParent(transform);
		string[] names = new string[GetCurrentFile().Data.Source.Bones.Length];
		string[] parents = new string[GetCurrentFile().Data.Source.Bones.Length];
		for(int i=0; i<GetCurrentFile().Data.Source.Bones.Length; i++) {
			names[i] = GetCurrentFile().Data.Source.Bones[i].Name;
			parents[i] = GetCurrentFile().Data.Source.Bones[i].Parent;
		}
		List<Transform> instances = new List<Transform>();
		for(int i=0; i<names.Length; i++) {
			Transform instance = new GameObject(names[i]).transform;
			instance.SetParent(parents[i] == "None" ? actor.GetRoot() : actor.FindTransform(parents[i]));
			instances.Add(instance);
		}
		actor.ExtractSkeleton(instances.ToArray());
		return actor;
	}

	public void CopyHierarchy()  {
		for(int i=0; i<GetCurrentFile().Data.Source.Bones.Length; i++) {
			if(GetActor().FindBone(GetCurrentFile().Data.Source.Bones[i].Name) != null) {
				GetCurrentFile().Data.Source.Bones[i].Active = true;
			} else {
				GetCurrentFile().Data.Source.Bones[i].Active = false;
			}
		}
	}

	public void Draw() {
		if(GetCurrentFile() == null) {
			return;
		}

		for(int i=0; i<GetCurrentFile().Data.Modules.Length; i++) {
			GetCurrentFile().Data.Modules[i].Draw(this);
		}
	}

	void OnRenderObject() {
		Draw();
	}

	void OnDrawGizmos() {
		if(!Application.isPlaying) {
			OnRenderObject();
		}
	}

	[CustomEditor(typeof(MotionEditor))]
	public class MotionEditor_Editor : Editor {

		public MotionEditor Target;

		private float RefreshRate = 30f;
		private System.DateTime Timestamp;

		public int Index = 0;
		public MotionFile[] Instances = new MotionFile[0];
		public string[] Names = new string[0];
		public string NameFilter = "";
		public bool ExportFilter = false;
		public bool ExcludeFilter = false;

		void Awake() {
			Target = (MotionEditor)target;
			Filter();
			Timestamp = Utility.GetTimestamp();
			EditorApplication.update += EditorUpdate;
		}

		void OnDestroy() {
			if(!Application.isPlaying && Target != null) {
				Target.SaveFile(Target.GetCurrentFile());
			}
			EditorApplication.update -= EditorUpdate;
		}

		public void EditorUpdate() {
			if(Utility.GetElapsedTime(Timestamp) >= 1f/RefreshRate) {
				Repaint();
				Timestamp = Utility.GetTimestamp();
			}
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);
			Inspector();
			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}
		
		private void Filter() {
			List<MotionFile> instances = new List<MotionFile>();
			if(NameFilter == string.Empty) {
				instances.AddRange(Target.Files);
			} else {
				for(int i=0; i<Target.Files.Length; i++) {
					if(Target.Files[i].Data.name.ToLowerInvariant().Contains(NameFilter.ToLowerInvariant())) {
						instances.Add(Target.Files[i]);
					}
				}
			}
			if(ExportFilter) {
				for(int i=0; i<instances.Count; i++) {
					if(!instances[i].Data.Export) {
						instances.RemoveAt(i);
						i--;
					}
				}
			}
			if(ExcludeFilter) {
				for(int i=0; i<instances.Count; i++) {
					if(instances[i].Data.Export) {
						instances.RemoveAt(i);
						i--;
					}
				}
			}
			Instances = instances.ToArray();
			Names = new string[Instances.Length];
			for(int i=0; i<Instances.Length; i++) {
				Names[i] = Instances[i].Data.name;
			}
			LoadFile(GetIndex());
		}

		public void SetNameFilter(string filter) {
			if(NameFilter != filter) {
				NameFilter = filter;
				Filter();
			}
		}

		public void SetExportFilter(bool value) {
			if(ExportFilter != value) {
				ExportFilter = value;
				Filter();
			}
		}

		public void SetExcludeFilter(bool value) {
			if(ExcludeFilter != value) {
				ExcludeFilter = value;
				Filter();
			}
		}

		public void LoadFile(int index) {
			if(Index != index) {
				Index = index;
				Target.LoadFile(Index >= 0 ? Target.Files[Instances[Index].GetIndex()] : null);
			}
		}

		public void Import() {
			Target.Import();
			Filter();
		}

		public void AutoProcess() {
			Target.AutoProcess();
		}
		public void AddFootContacts() {
			Target.AddFootContacts();
		}
		public void AddLocalPhases() {
			Target.AddLocalPhases();
		}
		public void AddJointPhases() {
			Target.AddJointPhases();
		}

		public void AugmentData() {
			Target.AugmentData();
		}

		public int GetIndex() {
			if(Target.GetCurrentFile() == null) {
				return -1;
			}
			if(Instances.Length == Target.Files.Length) {
				return Target.GetCurrentFile().GetIndex();
			} else {
				return System.Array.FindIndex(Instances, x => x == Target.GetCurrentFile());
			}
		}

		public void Inspector() {
			Index = GetIndex();

			Utility.SetGUIColor(UltiDraw.DarkGrey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.LightGrey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();

					EditorGUILayout.BeginHorizontal();
					Target.Folder = EditorGUILayout.TextField("Folder", "Assets/" + Target.Folder.Substring(Mathf.Min(7, Target.Folder.Length)));
					if(Utility.GUIButton("Import", UltiDraw.DarkGrey, UltiDraw.White)) {
						Import();
					}
					EditorGUILayout.EndHorizontal();

					Utility.SetGUIColor(Target.GetActor() == null ? UltiDraw.DarkRed : UltiDraw.White);
					Target.Actor = (Actor)EditorGUILayout.ObjectField("Actor", Target.GetActor(), typeof(Actor), true);
					Utility.ResetGUIColor();

					SetNameFilter(EditorGUILayout.TextField("Name Filter", NameFilter));
					EditorGUILayout.BeginHorizontal();
					SetExportFilter(EditorGUILayout.Toggle("Export Filter", ExportFilter));
					SetExcludeFilter(EditorGUILayout.Toggle("Exclude Filter", ExcludeFilter));
					EditorGUILayout.EndHorizontal();

					if(Instances.Length == 0) {
						LoadFile(-1);
						EditorGUILayout.LabelField("No data available.");
					} else if(Target.GetCurrentFile() == null) {
						LoadFile(0);
					} 
					if(Target.GetCurrentFile() != null && Target.GetCurrentScene() != null && Target.GetCurrentFrame() != null) {
						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField("Data", GUILayout.Width(50f));
							LoadFile(EditorGUILayout.Popup(Index, Names));
							if(Utility.GUIButton("<", UltiDraw.DarkGrey, UltiDraw.White)) {
								LoadFile(Mathf.Max(Index-1, 0));
							}
							if(Utility.GUIButton(">", UltiDraw.DarkGrey, UltiDraw.White)) {
								LoadFile(Mathf.Min(Index+1, Instances.Length-1));
							}
							LoadFile(EditorGUILayout.IntSlider(Index+1, 1, Instances.Length)-1);
							if(Utility.GUIButton("X", UltiDraw.DarkRed, UltiDraw.White, 60f, 18f)) {

							}
							EditorGUILayout.EndHorizontal();

							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField("Scene", GUILayout.Width(50f));
							Target.GetCurrentFile().LoadScene(EditorGUILayout.Popup(Target.GetCurrentScene().GetIndex(), Target.GetCurrentFile().GetSceneNames()));
							if(Utility.GUIButton("<", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.GetCurrentFile().LoadScene(Mathf.Max(Target.GetCurrentScene().GetIndex()-1, 0));
							}
							if(Utility.GUIButton(">", UltiDraw.DarkGrey, UltiDraw.White)) {
								Target.GetCurrentFile().LoadScene(Mathf.Min(Target.GetCurrentScene().GetIndex()+1, Target.GetCurrentFile().Scenes.Length-1));
							}
							Target.GetCurrentFile().LoadScene(EditorGUILayout.IntSlider(Target.GetCurrentScene().GetIndex()+1, 1, Target.GetCurrentFile().Scenes.Length)-1);
							if(Utility.GUIButton("+", UltiDraw.DarkGreen, UltiDraw.White, 28f, 18f)) {
								Target.GetCurrentFile().AddScene();
							}
							if(Utility.GUIButton("-", UltiDraw.DarkRed, UltiDraw.White, 28f, 18f)) {
								Target.GetCurrentFile().RemoveScene(Target.GetCurrentScene());
							}
							EditorGUILayout.EndHorizontal();

							EditorGUILayout.BeginHorizontal();
							if(Utility.GUIButton("Add Sequence", UltiDraw.DarkGrey, UltiDraw.White, 116f, 26f*Target.GetCurrentScene().Sequences.Length)) {
								Target.GetCurrentScene().AddSequence();
							}
							EditorGUILayout.BeginVertical();
							for(int i=0; i<Target.GetCurrentScene().Sequences.Length; i++) {
								Utility.SetGUIColor(UltiDraw.White);
								using(new EditorGUILayout.VerticalScope ("Box")) {
									Utility.ResetGUIColor();
									
									EditorGUILayout.BeginHorizontal();
									GUILayout.FlexibleSpace();
									if(Utility.GUIButton("X", Color.cyan, Color.black, 15f, 15f)) {
										Target.GetCurrentScene().Sequences[i].SetStart(Target.GetCurrentFrame().Index);
									}
									EditorGUILayout.LabelField("Start", GUILayout.Width(50f));
									Target.GetCurrentScene().Sequences[i].SetStart(Mathf.Clamp(EditorGUILayout.IntField(Target.GetCurrentScene().Sequences[i].Start, GUILayout.Width(100f)), 1, Target.GetCurrentFile().Data.GetTotalFrames()));
									EditorGUILayout.LabelField("End", GUILayout.Width(50f));
									Target.GetCurrentScene().Sequences[i].SetEnd(Mathf.Clamp(EditorGUILayout.IntField(Target.GetCurrentScene().Sequences[i].End, GUILayout.Width(100f)), 1, Target.GetCurrentFile().Data.GetTotalFrames()));
									if(Utility.GUIButton("X", Color.cyan, Color.black, 15f, 15f)) {
										Target.GetCurrentScene().Sequences[i].SetEnd(Target.GetCurrentFrame().Index);
									}
									GUILayout.FlexibleSpace();
									EditorGUILayout.EndHorizontal();
								}
							}
							EditorGUILayout.EndVertical();
							if(Utility.GUIButton("Remove Sequence", UltiDraw.DarkGrey, UltiDraw.White, 116f, 26f*Target.GetCurrentScene().Sequences.Length)) {
								Target.GetCurrentScene().RemoveSequence();
							}
							EditorGUILayout.EndHorizontal();
						}

						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							Frame frame = Target.GetCurrentFrame();

							Utility.SetGUIColor(UltiDraw.Mustard);
							using(new EditorGUILayout.VerticalScope ("Box")) {
								Utility.ResetGUIColor();
								EditorGUILayout.BeginHorizontal();
								GUILayout.FlexibleSpace();
								EditorGUILayout.LabelField(Target.GetCurrentFile().Data.name, GUILayout.Width(100f));
								EditorGUILayout.LabelField("Frames: " + Target.GetCurrentFile().Data.GetTotalFrames(), GUILayout.Width(100f));
								EditorGUILayout.LabelField("Time: " + Target.GetCurrentFile().Data.GetTotalTime().ToString("F3") + "s", GUILayout.Width(100f));
								EditorGUILayout.LabelField("Framerate: " + Target.GetCurrentFile().Data.Framerate.ToString("F1") + "Hz", GUILayout.Width(130f));
								EditorGUILayout.LabelField("Timescale:", GUILayout.Width(65f), GUILayout.Height(20f)); 
								Target.Timescale = EditorGUILayout.FloatField(Target.Timescale, GUILayout.Width(30f), GUILayout.Height(20f));
								if(Utility.GUIButton("Mirror", Target.Mirror ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
									Target.SetMirror(!Target.Mirror);
								}
								GUILayout.FlexibleSpace();
								EditorGUILayout.EndHorizontal();
							}

							Utility.SetGUIColor(UltiDraw.DarkGrey);
							using(new EditorGUILayout.VerticalScope ("Box")) {
								Utility.ResetGUIColor();
								EditorGUILayout.BeginHorizontal();
								GUILayout.FlexibleSpace();
								if(Target.Playing) {
									if(Utility.GUIButton("||", Color.red, Color.black, 20f, 20f)) {
										Target.StopAnimation();
									}
								} else {
									if(Utility.GUIButton("|>", Color.green, Color.black, 20f, 20f)) {
										Target.PlayAnimation();
									}
								}
								if(Utility.GUIButton("<<", UltiDraw.Grey, UltiDraw.White, 30f, 20f)) {
									Target.LoadFrame(Mathf.Max(Target.GetCurrentFrame().Index - Mathf.RoundToInt(Target.GetCurrentFile().Data.Framerate), 1));
								}
								if(Utility.GUIButton("<", UltiDraw.Grey, UltiDraw.White, 20f, 20f)) {
									Target.LoadPreviousFrame();
								}
								if(Utility.GUIButton(">", UltiDraw.Grey, UltiDraw.White, 20f, 20f)) {
									Target.LoadNextFrame();
								}
								if(Utility.GUIButton(">>", UltiDraw.Grey, UltiDraw.White, 30f, 20f)) {
									Target.LoadFrame(Mathf.Min(Target.GetCurrentFrame().Index + Mathf.RoundToInt(Target.GetCurrentFile().Data.Framerate), Target.GetCurrentFile().Data.GetTotalFrames()));
								}
								int index = EditorGUILayout.IntSlider(frame.Index, 1, Target.GetCurrentFile().Data.GetTotalFrames(), GUILayout.Width(440f));
								if(index != frame.Index) {
									Target.LoadFrame(index);
								}
								EditorGUILayout.LabelField(frame.Timestamp.ToString("F3") + "s", Utility.GetFontColor(Color.white), GUILayout.Width(50f));
								GUILayout.FlexibleSpace();
								EditorGUILayout.EndHorizontal();
								EditorGUILayout.BeginHorizontal();
								//GUILayout.FlexibleSpace();
								EditorGUILayout.LabelField("Window", Utility.GetFontColor(Color.white), GUILayout.Width(50f));
								Target.Window = EditorGUILayout.Slider(Target.Window, 0f, 1f);
								//GUILayout.FlexibleSpace();
								EditorGUILayout.EndHorizontal();
							}
						}
						for(int i=0; i<Target.GetCurrentFile().Data.Modules.Length; i++) {
							Target.GetCurrentFile().Data.Modules[i].Inspector(Target);
						}
						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							string[] modules = new string[(int)Module.TYPE.Length+1];
							modules[0] = "Add Module...";
							for(int i=1; i<modules.Length; i++) {
								modules[i] = ((Module.TYPE)(i-1)).ToString();
							}
							int module = EditorGUILayout.Popup(0, modules);
							if(module > 0) {
								Target.GetCurrentFile().Data.AddModule((Module.TYPE)(module-1));
							}
						}

						EditorGUILayout.BeginHorizontal();
						Target.CSV = EditorGUILayout.TextField("Semi-automatic processing: enter data CSV", Target.CSV);
						if(Utility.GUIButton("AutoProcess", UltiDraw.DarkGrey, UltiDraw.White)) {
							AutoProcess();
						}
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.BeginHorizontal();
						if(Utility.GUIButton("Add Foot Contacts", UltiDraw.DarkGrey, UltiDraw.White)) {
							AddFootContacts();
						}
						EditorGUILayout.EndHorizontal();

						EditorGUILayout.BeginHorizontal();
						if(Utility.GUIButton("Add Local Phases", UltiDraw.DarkGrey, UltiDraw.White)) {
							AddLocalPhases();
						}
						EditorGUILayout.EndHorizontal();

						// EditorGUILayout.BeginHorizontal();
						// if(Utility.GUIButton("AddJointPhases", UltiDraw.DarkGrey, UltiDraw.White)) {
						// 	AddJointPhases();
						// }
						// EditorGUILayout.EndHorizontal();

						EditorGUILayout.BeginHorizontal();
						EditorGUILayout.LabelField("T-pose Frame", GUILayout.Width(100f));
						Target.tpose_frame_idx = EditorGUILayout.IntField(Target.tpose_frame_idx);
						EditorGUILayout.EndHorizontal();

						EditorGUILayout.BeginHorizontal();
						if(Utility.GUIButton("Augment Data", UltiDraw.DarkGrey, UltiDraw.White)) {
							AugmentData();
						}
						EditorGUILayout.EndHorizontal();

						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							if(Utility.GUIButton("Camera Focus", Target.CameraFocus ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
								Target.SetCameraFocus(!Target.CameraFocus);
							}
							if(Target.CameraFocus) {
								Target.FocusHeight = EditorGUILayout.FloatField("Focus Height", Target.FocusHeight);
								Target.FocusOffset = EditorGUILayout.FloatField("Focus Offset", Target.FocusOffset);
								Target.FocusDistance = EditorGUILayout.FloatField("Focus Distance", Target.FocusDistance);
								Target.FocusAngle = EditorGUILayout.Slider("Focus Angle", Target.FocusAngle, 0f, 360f);
								Target.FocusSmoothing = EditorGUILayout.Slider("Focus Smoothing", Target.FocusSmoothing, 0f, 1f);
							}
						}

						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {
							Utility.ResetGUIColor();
							if(Utility.GUIButton("Settings", Target.InspectSettings ? UltiDraw.Cyan : UltiDraw.LightGrey, UltiDraw.Black)) {
								Target.InspectSettings = !Target.InspectSettings;
							}
							if(Target.InspectSettings) {
								Target.GetCurrentFile().Data.Export = EditorGUILayout.Toggle("Export", Target.GetCurrentFile().Data.Export);
								Target.SetScaling(EditorGUILayout.FloatField("Scaling", Target.GetCurrentFile().Data.Scaling));
								Target.GetCurrentFile().Data.MirrorAxis = (MotionData.AXIS)EditorGUILayout.EnumPopup("Mirror Axis", Target.GetCurrentFile().Data.MirrorAxis);
								string[] names = new string[Target.GetCurrentFile().Data.Source.Bones.Length];
								for(int i=0; i<Target.GetCurrentFile().Data.Source.Bones.Length; i++) {
									names[i] = Target.GetCurrentFile().Data.Source.Bones[i].Name;
								}
								for(int i=0; i<Target.GetCurrentFile().Data.Source.Bones.Length; i++) {
									EditorGUILayout.BeginHorizontal();
									if(Utility.GUIButton(Target.GetCurrentFile().Data.Source.Bones[i].Active ? "Active" : "Inactive", Target.GetCurrentFile().Data.Source.Bones[i].Active ? UltiDraw.DarkGreen : UltiDraw.DarkRed, UltiDraw.White, 60f, 16f)) {
										Target.GetCurrentFile().Data.Source.Bones[i].Active = !Target.GetCurrentFile().Data.Source.Bones[i].Active;
									}
									EditorGUI.BeginDisabledGroup(true);
									EditorGUILayout.TextField(names[i]);
									EditorGUI.EndDisabledGroup();
									Target.GetCurrentFile().Data.SetSymmetry(i, EditorGUILayout.Popup(Target.GetCurrentFile().Data.Symmetry[i], names));
									Target.GetCurrentFile().Data.Source.Bones[i].Alignment = EditorGUILayout.Vector3Field("", Target.GetCurrentFile().Data.Source.Bones[i].Alignment);
									EditorGUILayout.EndHorizontal();
								}
								EditorGUILayout.BeginHorizontal();
								/*
								if(Utility.GUIButton("Copy Hierarchy", UltiDraw.DarkGrey, UltiDraw.White)) {
									Target.CopyHierarchy();
								}
								if(Utility.GUIButton("Detect Symmetry", UltiDraw.DarkGrey, UltiDraw.White)) {
									Target.GetCurrentFile().Data.DetectSymmetry();
								}
								*/
								if(Utility.GUIButton("Create Skeleton", UltiDraw.DarkGrey, UltiDraw.White)) {
									Target.CreateSkeleton();
								}
								EditorGUILayout.EndHorizontal();
							}
						}
					}
				}
			}
		}
	}
}
#endif
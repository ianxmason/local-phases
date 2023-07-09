#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using DeepLearning;

public class MotionClipExporter : EditorWindow {

	public static EditorWindow Window;
	public static Vector2 Scroll;

	public string Directory = string.Empty;
	public string MasterDir = "Assets/Styles/";
	public int Framerate = 60;
	public int BatchSize = 60;
    public int ClipLength = 240;
	public string Info_CSV = "Assets/Data/Dataset_List.csv"; // string.Empty
	public string Frame_CSV = "Assets/Data/Frame_Cuts.csv"; // string.Empty
	public string ExportDir = string.Empty; // "/home/" or wherever export dir is

	public List<string> StyleNames = new List<string>();
	public List<bool> StyleExport = new List<bool>();

	public List<string> GaitNames = new List<string>();
	public List<bool> GaitExport = new List<bool>();

	public List<MotionFile> Files = new List<MotionFile>();
	public List<bool> FileExport = new List<bool>();
	public List<bool> DefaultExport = new List<bool>();
	public List<bool> MirrorExport = new List<bool>();
	public Dictionary<string, int[]> frameCuts = new Dictionary<string, int[]>();

	public MotionEditor Editor = null;

    private bool Exporting = false;
	private float Generating = 0f;
	private float Writing = 0f;

	// public bool WriteDefault = true;
	// public bool WriteMirror = true;
	public bool WriteData = true;
	public bool WriteNorm = false;
	public bool WriteLabels = true;
	public bool WriteGaits = true;
	public bool StylePhase = true;
	public bool StyleOneHot = false;
	public bool GaitPhase = false;
	public bool GaitOneHot = true;
	public bool ProcTogether = true;
	public bool ProcSeparate = false;
	public float TrainSplit = 80f;
	public float ValSplit = 10f;
	public float TestSplit = 10f;
	public bool SplitTransitions = false;

	public Data trainX;
	public Data trainY;
	public Data valX;
	public Data valY;
	public Data testX;
	public Data testY;
	public StreamWriter Tr_Va_Te_Frames;

	private static string Separator = " ";
	private static string Accuracy = "F5";

	[MenuItem ("Data Processing/Motion Clip Exporter")]
	static void Init() {
		Window = EditorWindow.GetWindow(typeof(MotionClipExporter));
		Scroll = Vector3.zero;
	}
	
	public void OnInspectorUpdate() {
		Repaint();
	}

	void OnGUI() {
		Scroll = EditorGUILayout.BeginScrollView(Scroll);

		if(Editor != GameObject.FindObjectOfType<MotionEditor>()) {
			Load();
		}

		// if(Editor == null) {
		// 	Utility.SetGUIColor(UltiDraw.Black);
		// 	using(new EditorGUILayout.VerticalScope ("Box")) {
		// 		Utility.ResetGUIColor();
		// 		Utility.SetGUIColor(UltiDraw.Grey);
		// 		using(new EditorGUILayout.VerticalScope ("Box")) {
		// 			Utility.ResetGUIColor();
		// 			Utility.SetGUIColor(UltiDraw.Orange);
		// 			using(new EditorGUILayout.VerticalScope ("Box")) {
		// 			Dictionary<string, int[]> frameCuts = new Dictionary<string, int[]>();
		// 			Dictionary<string, int[]> frameCuts = new Dictionary<string, int[]>();
		// 			}Dictionary<string, int[]> frameCuts = new Dictionary<string, int[]>();
		// 			EditorGUILayout.LabelField("No Motion Editor found in scene.");
		// 		}
		// 	}
		// } else {
		Utility.SetGUIColor(UltiDraw.Black);
		using(new EditorGUILayout.VerticalScope ("Box")) {
			Utility.ResetGUIColor();

			Utility.SetGUIColor(UltiDraw.Grey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.Orange);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					EditorGUILayout.LabelField("Exporter");
				}

				MasterDir = EditorGUILayout.TextField("Master Directory (end with /)", MasterDir);
				ExportDir = EditorGUILayout.TextField("Export Directory (end with /)", ExportDir); 
				Framerate = EditorGUILayout.IntField("Framerate", Framerate);
				BatchSize = Mathf.Max(1, EditorGUILayout.IntField("Batch Size", BatchSize));
                ClipLength = EditorGUILayout.IntField("Clip Length", ClipLength);
				Info_CSV = EditorGUILayout.TextField("Data Info CSV", Info_CSV); 
				Frame_CSV = EditorGUILayout.TextField("Frame Cuts CSV", Frame_CSV); 
				WriteData = EditorGUILayout.Toggle("Write Data", WriteData);
				WriteLabels = EditorGUILayout.Toggle("Write Labels", WriteLabels);
				WriteNorm = EditorGUILayout.Toggle("Write Norm", WriteNorm);
				WriteGaits = EditorGUILayout.Toggle("Write Gaits", WriteGaits);
				// WriteDefault = EditorGUILayout.Toggle("Write Default", WriteDefault);
				// WriteMirror = EditorGUILayout.Toggle("Write Mirror", WriteMirror);
				EditorGUILayout.BeginHorizontal();
				TrainSplit = EditorGUILayout.FloatField("Train Split", TrainSplit, GUILayout.Width(200f));
				ValSplit = EditorGUILayout.FloatField("Val Split", ValSplit, GUILayout.Width(200f));
				TestSplit = EditorGUILayout.FloatField("Test Split", TestSplit, GUILayout.Width(200f));
				SplitTransitions = EditorGUILayout.Toggle("Split Transitions", SplitTransitions, GUILayout.Width(200f));
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.LabelField("Choose one for each row (only first box is toggleable):");
				EditorGUILayout.BeginHorizontal();
				StylePhase = EditorGUILayout.Toggle("Style Phase Combination", StylePhase, GUILayout.Width(400f));
				StyleOneHot = EditorGUILayout.Toggle("One Hot Label", !StylePhase, GUILayout.Width(400f));
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GaitPhase = EditorGUILayout.Toggle("Gait Phase Combination", GaitPhase, GUILayout.Width(400f));
				GaitOneHot = EditorGUILayout.Toggle("One Hot Label", !GaitPhase, GUILayout.Width(400f));
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				ProcTogether = EditorGUILayout.Toggle("Process all styles together", ProcTogether, GUILayout.Width(400f));
				ProcSeparate = EditorGUILayout.Toggle("Process each style separately", !ProcTogether, GUILayout.Width(400f));
				EditorGUILayout.EndHorizontal();

				Utility.SetGUIColor(UltiDraw.LightGrey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					Utility.SetGUIColor(UltiDraw.Cyan);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						EditorGUILayout.LabelField("Files");
					}
					if(Files.Count == 0) {
						EditorGUILayout.LabelField("No files found.");
					} else {
						EditorGUILayout.BeginHorizontal();
						if(Utility.GUIButton("Enable All", UltiDraw.DarkGrey, UltiDraw.White)) {
							for(int i=0; i<Files.Count; i++) {
								FileExport[i] = true;
							}
						}
						if(Utility.GUIButton("Disable All", UltiDraw.DarkGrey, UltiDraw.White)) {
							for(int i=0; i<Files.Count; i++) {
								FileExport[i] = false;
							}
						}
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.BeginHorizontal();
						EditorGUILayout.LabelField("Export File", GUILayout.Width(250f));
						EditorGUILayout.LabelField("Write Default", GUILayout.Width(100f));
						EditorGUILayout.LabelField("Write Mirror", GUILayout.Width(100f));
						EditorGUILayout.EndHorizontal();
						for(int i=0; i<Files.Count; i++) {
							EditorGUILayout.BeginHorizontal();
							FileExport[i] = EditorGUILayout.Toggle(Files[i].name, FileExport[i], GUILayout.Width(250f));
							DefaultExport[i] = EditorGUILayout.Toggle(DefaultExport[i], GUILayout.Width(100f));
							MirrorExport[i] = EditorGUILayout.Toggle(MirrorExport[i], GUILayout.Width(100f));
							EditorGUILayout.EndHorizontal();
						}
					}
				}

				Utility.SetGUIColor(UltiDraw.LightGrey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					Utility.SetGUIColor(UltiDraw.Cyan);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						EditorGUILayout.LabelField("Gaits");
					}
					if(GaitNames.Count == 0) {
						EditorGUILayout.LabelField("No gaits found.");
					} else {
						for(int i=0; i<GaitNames.Count; i++) {
							GaitExport[i] = EditorGUILayout.Toggle(GaitNames[i], GaitExport[i]);
						}
					}
				}

				Utility.SetGUIColor(UltiDraw.LightGrey);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					Utility.SetGUIColor(UltiDraw.Cyan);
					using(new EditorGUILayout.VerticalScope ("Box")) {
						Utility.ResetGUIColor();
						EditorGUILayout.LabelField("Styles");
					}
					if(StyleNames.Count == 0) {
						EditorGUILayout.LabelField("No styles found.");
					} else {
						for(int i=0; i<StyleNames.Count; i++) {
							StyleExport[i] = EditorGUILayout.Toggle(StyleNames[i], StyleExport[i]);
						}
					}
				}

				if(!Exporting) {
					if(Utility.GUIButton("Reload", UltiDraw.DarkGrey, UltiDraw.White)) {
						Load();
					}
					if(Utility.GUIButton("Export Data", UltiDraw.DarkGrey, UltiDraw.White)) {
						trainX = new Data(WriteData ? CreateFile("InputTrainClips") : null, WriteNorm ? CreateFile("InputNormClips") : null, null);
						valX = new Data(WriteData ? CreateFile("InputValClips") : null, null, WriteLabels ? CreateFile("InputLabelsClips") : null);
						testX = new Data(WriteData ? CreateFile("InputTestClips") : null, null, null);
						if(ProcTogether){
							this.StartCoroutine(ExportData());
						}
						if(ProcSeparate){
							this.StartCoroutine(ExportDataSeparately());
						}
					}
					if(Utility.GUIButton("Export Data From Subdirectories", UltiDraw.DarkGrey, UltiDraw.White)) {
						trainX = new Data(WriteData ? CreateFile("InputTrainClips") : null, WriteNorm ? CreateFile("InputNormClips") : null, null);
						valX = new Data(WriteData ? CreateFile("InputValClips") : null, null, WriteLabels ? CreateFile("InputLabelsClips") : null);
						testX = new Data(WriteData ? CreateFile("InputTestClips") : null, null, null);
						Tr_Va_Te_Frames = CreateFile("Tr_Va_Te_Clips");
						this.StartCoroutine(ExportSubdirectoryData());

						// DirectoryInfo info = new DirectoryInfo(MasterDir);
						// DirectoryInfo[] subdirectories = info.GetDirectories();
						// for(int j=0; j<subdirectories.Length; j++) {
						// 	FileInfo[] items = subdirectories[j].GetFiles("*.unity");
						// 	if(items.Length != 1){
						// 		Debug.Log("Problem loading scene - either no scene or too many scenes detected");
						// 	}
						// 	else{
						// 		EditorSceneManager.OpenScene(items[0].DirectoryName + "/" + items[0].Name);
						// 		Load();
						// 		if(ProcTogether){
						// 			this.StartCoroutine(ExportData());
						// 		}
						// 		if(ProcSeparate){
						// 			this.StartCoroutine(ExportDataSeparately());
						// 		}
						// 	}								
						// }
					}

					//if(Utility.GUIButton("Test", UltiDraw.DarkGrey, UltiDraw.White)) {
					//	Debug.Log(Mathf.Repeat(-0.1f, 1f));
						//float[] angles = new float[6]{0f, 0.4f, 0.6f, 1.0f, 1.2f, 1.4f};
						//Debug.Log(Utility.FilterGaussian(angles, true));
					//}
				} else {
					EditorGUILayout.LabelField("File: " + Editor.GetCurrentFile().Data.name);

					EditorGUILayout.LabelField("Generating");
					EditorGUI.DrawRect(new Rect(EditorGUILayout.GetControlRect().x, EditorGUILayout.GetControlRect().y, Generating * EditorGUILayout.GetControlRect().width, 25f), UltiDraw.Green.Transparent(0.75f));

					EditorGUILayout.LabelField("Writing");
					EditorGUI.DrawRect(new Rect(EditorGUILayout.GetControlRect().x, EditorGUILayout.GetControlRect().y, Writing * EditorGUILayout.GetControlRect().width, 25f), UltiDraw.Green.Transparent(0.75f));

					if(Utility.GUIButton("Stop", UltiDraw.DarkRed, UltiDraw.White)) {
						this.StopAllCoroutines();
						Exporting = false;
					}
				}
			}
		}
		// }

		EditorGUILayout.EndScrollView();
	}

	public void Load() {
		Editor = GameObject.FindObjectOfType<MotionEditor>();

		StyleNames = new List<string>();
		StyleExport = new List<bool>();
		GaitNames = new List<string>();
		GaitExport = new List<bool>();

		Files = new List<MotionFile>();
		FileExport = new List<bool>();
		DefaultExport = new List<bool>();
		MirrorExport = new List<bool>();
		frameCuts = new Dictionary<string, int[]>();

		// /home/ian/AI4AnimationStyle/Data_Information/Dataset_List.csv
		string[] lines = File.ReadAllLines(Info_CSV);
		Dictionary<string, bool> symmetry = new Dictionary<string, bool>();
		bool firstLine = true;
		foreach(string line in lines){
			if(!firstLine){
				if(line.Split(',')[3] == "No"){
					symmetry.Add(line.Split(',')[0], false);
				}
				else if(line.Split(',')[3] == "Yes"){
					symmetry.Add(line.Split(',')[0], true);
				}
			}
			firstLine = false;			
		}

		// /home/ian/AI4AnimationStyle/Data_Information/Frame_Cuts.csv
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

		for(int i=0; i<Editor.Files.Length; i++) {
			if(Editor.Files[i].Data.Export) {
				Files.Add(Editor.Files[i]);
				// No Idle Export:
				if(Editor.Files[i].Data.name.Split('_')[1]=="ID.bvh"){
					FileExport.Add(false);
					DefaultExport.Add(false);  
					MirrorExport.Add(false);
				}
				else{
					FileExport.Add(true);
					DefaultExport.Add(true);  
					MirrorExport.Add(symmetry[Editor.Files[i].Data.name.Split('_')[0]]); // CSV tells us if a style is symmetric
				}					
			}
			if(Editor.Files[i].Data.GetModule(Module.TYPE.Style) != null) {
				StyleModule module = (StyleModule)Editor.Files[i].Data.GetModule(Module.TYPE.Style);
				for(int j=0; j<module.Functions.Length; j++) {
					if(!StyleNames.Contains(module.Functions[j].Name)) {
						StyleNames.Add(module.Functions[j].Name);
						// Uncomment the below and comment out the final StyleExport.Add(true); to export only 10 styles. Also uncomment in ExportSubdirectoryData
						// if(!(module.Functions[j].Name=="CrossOver" || module.Functions[j].Name=="Flapping" || module.Functions[j].Name=="HandsBetweenLegs" ||
						// module.Functions[j].Name=="Neutral" || module.Functions[j].Name=="Old" // || module.Functions[j].Name=="Roadrunner" ||
						// // module.Functions[j].Name=="ShieldedLeft" || module.Functions[j].Name=="Skip" || module.Functions[j].Name=="Star" ||
						// // module.Functions[j].Name=="WildArms"
						// ))
						// if(!(module.Functions[j].Name=="Roadrunner" || module.Functions[j].Name=="ShieldedLeft" || module.Functions[j].Name=="Skip"
						// || module.Functions[j].Name=="Star" || module.Functions[j].Name=="WildArms"
						// )){
						// 	StyleExport.Add(false);
						// }
						// else{
						// 	StyleExport.Add(true);
						// }

						// if(!(module.Functions[j].Name=="Aeroplane" || module.Functions[j].Name=="Akimbo" || module.Functions[j].Name=="Angry" ||
						// module.Functions[j].Name=="ArmsAboveHead" || module.Functions[j].Name=="ArmsBehindBack" || module.Functions[j].Name=="ArmsBySide" ||
						// module.Functions[j].Name=="ArmsFolded" || module.Functions[j].Name=="Balance" || module.Functions[j].Name=="BeatChest" ||
						// module.Functions[j].Name=="BentForward" || module.Functions[j].Name=="BentKnees" || module.Functions[j].Name=="BigSteps" ||
						// module.Functions[j].Name=="BouncyLeft" || module.Functions[j].Name=="BouncyRight" || module.Functions[j].Name=="Cat" ||
						// module.Functions[j].Name=="Chicken" || module.Functions[j].Name=="CrossOver" || module.Functions[j].Name=="Crouched" ||
						// module.Functions[j].Name=="CrowdAvoidance" || module.Functions[j].Name=="Depressed" || module.Functions[j].Name=="Dinosaur" ||
						// module.Functions[j].Name=="DragLeftLeg" || module.Functions[j].Name=="DragRightLeg" || module.Functions[j].Name=="Drunk" ||
						// module.Functions[j].Name=="DuckFoot"
						// )){
						// 	StyleExport.Add(false);
						// }
						// else{
						// 	StyleExport.Add(true);
						// }


						// 95 style FW only export. Add ! to export corresponding 5 styles
						if((module.Functions[j].Name=="Roadrunner" || module.Functions[j].Name=="ShieldedLeft" || module.Functions[j].Name=="Skip"
						|| module.Functions[j].Name=="Star" || module.Functions[j].Name=="WildArms"
						)){
							StyleExport.Add(false);
						}
						else{
							StyleExport.Add(true);
						}

						// 100 Styles
						// StyleExport.Add(true);
					}
				}
			}
			if(Editor.Files[i].Data.GetModule(Module.TYPE.Gait) != null) {
				GaitModule module = (GaitModule)Editor.Files[i].Data.GetModule(Module.TYPE.Gait);
				for(int j=0; j<module.Functions.Length; j++) {
					if(!GaitNames.Contains(module.Functions[j].Name)) {
						GaitNames.Add(module.Functions[j].Name);
						GaitExport.Add(true);
					}
				}
			}
		}
	}

	private StreamWriter CreateFile(string name) {
		string filename = string.Empty;
		string folder = ExportDir; // Application.dataPath + "/../../Export/";
		if(!File.Exists(folder+name+".txt")) {
			filename = folder+name;
		} else {
			int i = 1;
			while(File.Exists(folder+name+" ("+i+").txt")) {
				i += 1;
			}
			filename = folder+name+" ("+i+")";
		}
		return File.CreateText(filename+".txt");
	}

	

	private IEnumerator ExportSubdirectoryData() {
		// if(Editor == null) {
		// 	Debug.Log("No editor found.");
		if(TestSplit+ValSplit+TrainSplit != 100f){
			Debug.Log("Data splits do not sum to 100");
		} else {
			DirectoryInfo info = new DirectoryInfo(MasterDir);
			DirectoryInfo[] subdirectories = info.GetDirectories();
			for(int b=0; b<subdirectories.Length; b++) {
				FileInfo[] sceneItems = subdirectories[b].GetFiles("*.unity");
				if(sceneItems.Length != 1){
					Debug.Log("Problem loading scene - either no scene or too many scenes detected");
				}
				// Uncomment below to extract only certain styles for smaller dataset. Also uncomment in Load function to export only relevant style labels.
				// else if(!(sceneItems[0].Name == "StyleData 16.unity" || sceneItems[0].Name == "StyleData 27.unity" || sceneItems[0].Name == "StyleData 31.unity" || 
				// 		sceneItems[0].Name == "StyleData 51.unity" || sceneItems[0].Name == "StyleData 52.unity" // || sceneItems[0].Name == "StyleData 67.unity" || 
				// 		// sceneItems[0].Name == "StyleData 71.unity" || sceneItems[0].Name == "StyleData 73.unity" || sceneItems[0].Name == "StyleData 77.unity" || 
				// 		// sceneItems[0].Name == "StyleData 97.unity"
				// 		))
				// else if(!(sceneItems[0].Name == "StyleData 67.unity" || sceneItems[0].Name == "StyleData 71.unity" || sceneItems[0].Name == "StyleData 73.unity" 
				// 		|| sceneItems[0].Name == "StyleData 77.unity" || sceneItems[0].Name == "StyleData 97.unity"
				// 		)){
				// 	Debug.Log("Only extracting 10 style dataset, ignoring scene: " + sceneItems[0].Name);
				// 	// 16 - crossover, 27 - flapping, 31 - handsbetweenlegs, 51 - neutral, 52 - old
				// 	// 67 - roadrunner, 71 - shielded left, 73 - skip, 77 - star, 97 - wild arms
				// }
				// else if(!(sceneItems[0].Name == "StyleData.unity" || sceneItems[0].Name == "StyleData 1.unity" || sceneItems[0].Name == "StyleData 2.unity" || 
				// 		sceneItems[0].Name == "StyleData 3.unity" || sceneItems[0].Name == "StyleData 4.unity" || sceneItems[0].Name == "StyleData 5.unity" || 
				// 		sceneItems[0].Name == "StyleData 6.unity" || sceneItems[0].Name == "StyleData 7.unity" || sceneItems[0].Name == "StyleData 8.unity" || 
				// 		sceneItems[0].Name == "StyleData 9.unity" || sceneItems[0].Name == "StyleData 10.unity" || sceneItems[0].Name == "StyleData 11.unity" || 
				// 		sceneItems[0].Name == "StyleData 12.unity" || sceneItems[0].Name == "StyleData 13.unity" || sceneItems[0].Name == "StyleData 14.unity" || 
				// 		sceneItems[0].Name == "StyleData 15.unity" || sceneItems[0].Name == "StyleData 16.unity" || sceneItems[0].Name == "StyleData 17.unity" || 
				// 		sceneItems[0].Name == "StyleData 18.unity" || sceneItems[0].Name == "StyleData 19.unity" || sceneItems[0].Name == "StyleData 20.unity" || 
				// 		sceneItems[0].Name == "StyleData 21.unity" || sceneItems[0].Name == "StyleData 22.unity" || sceneItems[0].Name == "StyleData 23.unity" || 
				// 		sceneItems[0].Name == "StyleData 24.unity")){
				// 	Debug.Log("Only extracting 10 style dataset, ignoring scene: " + sceneItems[0].Name);
				// 	}
				else if((sceneItems[0].Name == "StyleData 67.unity" || sceneItems[0].Name == "StyleData 71.unity" || sceneItems[0].Name == "StyleData 73.unity" 
						|| sceneItems[0].Name == "StyleData 77.unity" || sceneItems[0].Name == "StyleData 97.unity"
						)){
					Debug.Log("Extracting 95 style dataset, ignoring scene: " + sceneItems[0].Name);
					// 16 - crossover, 27 - flapping, 31 - handsbetweenlegs, 51 - neutral, 52 - old
					// 67 - roadrunner, 71 - shielded left, 73 - skip, 77 - star, 97 - wild arms
				}
				else{
					EditorSceneManager.OpenScene(sceneItems[0].DirectoryName + "/" + sceneItems[0].Name);
					Load();
				
					Exporting = true;

					Generating = 0f;
					Writing = 0f;

					UnityEngine.Random.InitState(12345); // seed Random to ensure same results over different exports

					int items = 0;
					int frameCutsIndex = 0;

					// Data trainX = new Data(WriteData ? CreateFile("InputTrain") : null, WriteNorm ? CreateFile("InputNorm") : null, null);
					// Data trainY = new Data(WriteData ? CreateFile("OutputTrain") : null, WriteNorm ? CreateFile("OutputNorm") : null, null);
					// Data valX = new Data(WriteData ? CreateFile("InputVal") : null, null, WriteLabels ? CreateFile("InputLabels") : null);
					// Data valY = new Data(WriteData ? CreateFile("OutputVal") : null, null, WriteLabels ? CreateFile("OutputLabels") : null);
					// Data testX = new Data(WriteData ? CreateFile("InputTest") : null, null, null);
					// Data testY = new Data(WriteData ? CreateFile("OutputTest") : null, null, null);

					for(int i=0; i<Files.Count; i++) {
						// if(FileExport[i]) {
						// For 95 style FW only
						if(Editor.Files[i].name.Substring(Editor.Files[i].name.Length - 6, 2) == "FW"){
							Editor.LoadFile(Files[i]);

							Editor.GetCurrentFile().LoadScene(0);
							string current_gait = Editor.GetCurrentFile().Data.name.Split('_')[1].Split('.')[0];
							string current_name = Editor.GetCurrentFile().Data.name.Split('_')[0];
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
							int startFrame = frameCuts[current_name][frameCutsIndex];
							int endFrame = frameCuts[current_name][frameCutsIndex+1];
							int valWindow = (int)Mathf.Floor((ValSplit/100f) * (endFrame-startFrame)); // no of validation frames
							int testWindow = (int)Mathf.Floor((TestSplit/100f) * (endFrame-startFrame)); // no of test frames
							int valStartFrame = UnityEngine.Random.Range(startFrame, endFrame - valWindow);
							int testStartFrame;
							
							if(valStartFrame - testWindow <= startFrame){
								testStartFrame = UnityEngine.Random.Range(valStartFrame + valWindow, endFrame - testWindow);
							}
							else if(valStartFrame+valWindow >= endFrame - testWindow){
								testStartFrame = UnityEngine.Random.Range(startFrame, valStartFrame);
							}
							else{
								int coin = UnityEngine.Random.Range(0,2);
								if(coin==0){
									testStartFrame = UnityEngine.Random.Range(valStartFrame + valWindow, endFrame - testWindow);
								}
								else if(coin==1){
									testStartFrame = UnityEngine.Random.Range(startFrame, valStartFrame);
								}
								else{
									Debug.Log("Rand Error - Data not split into train/val/test!");
									testStartFrame = -1;
								}
							}

							FrameSplit[] frameSplits = new FrameSplit[6] {new FrameSplit("startFrame", startFrame), new FrameSplit("valStartFrame", valStartFrame),
														new FrameSplit("testStartFrame", testStartFrame), new FrameSplit("endFrame", endFrame),
														new FrameSplit("valEndFrame", valStartFrame+valWindow), new FrameSplit("testEndFrame", testStartFrame+testWindow)};
							if(Tr_Va_Te_Frames != null) {
								if(current_gait == "TR" || current_gait == "TR1" || current_gait == "TR2" || current_gait == "TR3"){
									if(SplitTransitions){
										string[] towrite = new string[9];
										towrite[0] = current_name + "_" + current_gait + ".";
										towrite[1] = "Validation Frames:";
										towrite[2] = valStartFrame.ToString();
										towrite[3] = "to";
										towrite[4] = (valStartFrame+valWindow).ToString() + ".";
										towrite[5] = "Test Frames:";
										towrite[6] = testStartFrame.ToString();
										towrite[7] = "to";
										towrite[8] = (testStartFrame+testWindow).ToString() + ".";
										string writeline = System.String.Join(Separator, towrite);
										Tr_Va_Te_Frames.WriteLine(writeline);
									}
								}
								else{
									string[] towrite = new string[9];
									towrite[0] = current_name + "_" + current_gait + ".";
									towrite[1] = "Validation Frames:";
									towrite[2] = valStartFrame.ToString();
									towrite[3] = "to";
									towrite[4] = (valStartFrame+valWindow).ToString() + ".";
									towrite[5] = "Test Frames:";
									towrite[6] = testStartFrame.ToString();
									towrite[7] = "to";
									towrite[8] = (testStartFrame+testWindow).ToString() + ".";
									string writeline = System.String.Join(Separator, towrite);
									Tr_Va_Te_Frames.WriteLine(writeline);
								}
								
							}
							Array.Sort(frameSplits);

							for(int m=1; m<=2; m++) {
								if(m==1) {
									if(DefaultExport[i]) {
										Editor.SetMirror(false);
									} else {
										continue;
									}
								}
								if(m==2) {
									if(MirrorExport[i]) {
										Editor.SetMirror(true);
									} else {
										continue;
									}
								}

								for(int n=0; n<frameSplits.Length-1; n++){
									//Generating
									List<State> states = new List<State>();
									float start = Editor.GetCurrentFile().Data.GetFrame(frameSplits[n].FrameNumber).Timestamp;
									float end = Editor.GetCurrentFile().Data.GetFrame(frameSplits[n+1].FrameNumber).Timestamp;
									for(float t=start; t<=end; t+=1f/Framerate) {
										Editor.LoadFrame(t);
										states.Add(new State(Editor));

										Generating = (t-start) / (end-start);

										items += 1;
										if(items == BatchSize) {
											items = 0;
											yield return new WaitForSeconds(0f);
										}
									}

									//Precomputations
									string[] styles = GetActiveStyles();
									int[] stylemapping = GetMapping(states[0].Trajectory.Styles, styles);
									string[] gaits = GetActiveGaits();
									int[] gaitmapping = GetMapping(states[0].Trajectory.Gaits, gaits);
									Data X; // Empty variable used for easier code reading

									if(current_gait == "TR" || current_gait == "TR1" || current_gait == "TR2" || current_gait == "TR3"){
										if(SplitTransitions && frameSplits[n].Name == "valStartFrame"){
											X = valX;
										}
										else if(SplitTransitions && frameSplits[n].Name == "testStartFrame"){
											X = testX;
										}
										else{
											X = trainX;
										}
									}
									else{
										if(frameSplits[n].Name == "valStartFrame"){
											X = valX;
										}
										else if(frameSplits[n].Name == "testStartFrame"){
											X = testX;
										}
										else{
											X = trainX;
										}
									}
									

                                    for(int j=ClipLength; j<states.Count; j+=ClipLength/2) {  // overlapping window size ClipLength/2
                                        // State[] statesWindow = new State[ClipLength];
                                        for(int q=0; q<ClipLength; q++){
                                            // statesWindow[q] = states[j+q];
                                            State current = states[j-ClipLength+q];
                                            //Writing
                                            //Input
                                            for(int k=0; k<12; k++) {
                                                X.FeedXZ(current.Trajectory.Points[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Trajectory"+(k+1)+"Position");
                                                X.FeedXZ(current.Trajectory.Points[k].GetDirection().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Trajectory"+(k+1)+"Direction");
                                            }
                                            for(int k=0; k<current.Posture.Length; k++) {
												if(k==7 || k==22 || k==27){
													// Debug.Log("Only exporting 25 bones ignoring bone " + k);
													// Temporary to check correct bones are ignored
													// Debug.Log("Bone Name:" + Editor.GetActor().Bones[k].GetName()); 
													continue;
												}
												else{
													X.Feed(current.Posture[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Position");
													// X.Feed(current.Posture[k].GetRotation().GetRelativeRotationTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Rotation");
													X.Feed(current.Posture[k].GetForward().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Forward");
                                                	X.Feed(current.Posture[k].GetUp().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Up");
													X.Feed(current.Velocities[k].GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Velocity");
												}
                                                // X.Feed(current.Posture[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Position");
                                                // // X.Feed(current.Posture[k].GetForward().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Forward");
                                                // // X.Feed(current.Posture[k].GetUp().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Up");
												// X.Feed(current.Posture[k].GetRotation().GetRelativeRotationTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Rotation");
                                                // X.Feed(current.Velocities[k].GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Frame" + (q+1) + "Bone"+(k+1)+"Velocity");
                                            }										

                                            for(int k=6; k<7; k++) { // 6 is current
                                                X.Feed(current.Trajectory.Points[k].Phase, Data.ID.Standard, "Frame" + (q+1) + "Phase");

                                                float[] filteredGaits = Filter(current.Trajectory.Points[k].Gaits, gaitmapping, gaits.Length);
                                                for(int gait=0; gait<gaits.Length; gait++) {
                                                    X.Feed(filteredGaits[gait], Data.ID.Standard, "Frame" + (q+1) + "GaitOneHot -"+gait);
                                                }

                                                float[] filteredStyles = Filter(current.Trajectory.Points[k].Styles, stylemapping, styles.Length);
                                                for(int style=0; style<styles.Length; style++) {
                                                    X.Feed(filteredStyles[style], Data.ID.Standard, "Frame" + (q+1) + "StyleOneHot -"+style);
                                                }
                                            }
                                        }

										X.Store();  
                                        
                                        // This is not correct anymore - was only valid when we did for int j=0; j< states.Count-1; j++
                                        // Probably should decrease batchsize to 5 or something
										Writing = (float)j / (float)(states.Count-1);

										items += 1;
										if(items == BatchSize) {
											items = 0;
											yield return new WaitForSeconds(0f);
										}
									}
								}
							}
						}
					}
				}
			}

			trainX.Finish();
			valX.Finish();
			testX.Finish();
			Tr_Va_Te_Frames.Close();

			Exporting = false;
			yield return new WaitForSeconds(0f);
		}
	}

	private IEnumerator ExportData() {
		if(Editor == null) {
			Debug.Log("No editor found.");
		} else if(TestSplit+ValSplit+TrainSplit != 100f){
			Debug.Log("Data splits do not sum to 100");
		} else {
			Debug.Log("Export Data not currently correct use export from subdirectories");
			yield return new WaitForSeconds(0f);
		}
		// } else {
		// 	Exporting = true;

		// 	Generating = 0f;
		// 	Writing = 0f;

		// 	UnityEngine.Random.InitState(12345); // seed Random to ensure same results over different exports

		// 	int items = 0;

		// 	// Data trainX = new Data(WriteData ? CreateFile("InputTrain") : null, WriteNorm ? CreateFile("InputNorm") : null, null);
		// 	// Data trainY = new Data(WriteData ? CreateFile("OutputTrain") : null, WriteNorm ? CreateFile("OutputNorm") : null, null);
		// 	// Data valX = new Data(WriteData ? CreateFile("InputVal") : null, null, WriteLabels ? CreateFile("InputLabels") : null);
		// 	// Data valY = new Data(WriteData ? CreateFile("OutputVal") : null, null, WriteLabels ? CreateFile("OutputLabels") : null);
		// 	// Data testX = new Data(WriteData ? CreateFile("InputTest") : null, null, null);
		// 	// Data testY = new Data(WriteData ? CreateFile("OutputTest") : null, null, null);

		// 	for(int i=0; i<Files.Count; i++) {
		// 		if(FileExport[i]) {
		// 			Editor.LoadFile(Files[i]);

		// 			Editor.GetCurrentFile().LoadScene(0);
		// 			string current_gait = Editor.GetCurrentFile().Data.name.Split('_')[1].Split('.')[0];
		// 			int startFrame = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[0].Start).Index;
		// 			int endFrame = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[0].End).Index;
		// 			int valWindow = (int)Mathf.Floor((ValSplit/100f) * (endFrame-startFrame)); // no of validation frames
		// 			int testWindow = (int)Mathf.Floor((TestSplit/100f) * (endFrame-startFrame)); // no of test frames
		// 			int valStartFrame = UnityEngine.Random.Range(startFrame, endFrame - valWindow);
		// 			int testStartFrame;
					
		// 			if(valStartFrame - testWindow <= startFrame){
		// 				testStartFrame = UnityEngine.Random.Range(valStartFrame + valWindow, endFrame - testWindow);
		// 			}
		// 			else if(valStartFrame+valWindow >= endFrame - testWindow){
		// 				testStartFrame = UnityEngine.Random.Range(startFrame, valStartFrame);
		// 			}
		// 			else{
		// 				int coin = UnityEngine.Random.Range(0,2);
		// 				if(coin==0){
		// 					testStartFrame = UnityEngine.Random.Range(valStartFrame + valWindow, endFrame - testWindow);
		// 				}
		// 				else if(coin==1){
		// 					testStartFrame = UnityEngine.Random.Range(startFrame, valStartFrame);
		// 				}
		// 				else{
		// 					Debug.Log("Rand Error - Data not split into train/val/test!");
		// 					testStartFrame = -1;
		// 				}
		// 			}

		// 			FrameSplit[] frameSplits = new FrameSplit[6] {new FrameSplit("startFrame", startFrame), new FrameSplit("valStartFrame", valStartFrame),
		// 										new FrameSplit("testStartFrame", testStartFrame), new FrameSplit("endFrame", endFrame),
		// 										new FrameSplit("valEndFrame", valStartFrame+valWindow), new FrameSplit("testEndFrame", testStartFrame+testWindow)};
		// 			Array.Sort(frameSplits);

		// 			for(int m=1; m<=2; m++) {
		// 				if(m==1) {
		// 					if(DefaultExport[i]) {
		// 						Editor.SetMirror(false);
		// 					} else {
		// 						continue;
		// 					}
		// 				}
		// 				if(m==2) {
		// 					if(MirrorExport[i]) {
		// 						Editor.SetMirror(true);
		// 					} else {
		// 						continue;
		// 					}
		// 				}
		// 				// for(int scene=0; scene<Editor.GetCurrentFile().Scenes.Length; scene++) { // we only have one scene per file - different scenes can be used for adding different obstacles
		// 				// 	Editor.GetCurrentFile().LoadScene(scene);
		// 				// for(int sequence=0; sequence<Editor.GetCurrentScene().Sequences.Length; sequence++) { // we will also only allow one sequence
		// 				// List<State> states = new List<State>();
		// 				// float start = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[sequence].Start).Timestamp;
		// 				// float end = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[sequence].End).Timestamp;
		// 				// float start = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[0].Start).Timestamp;
		// 				// float end = Editor.GetCurrentFile().Data.GetFrame(Editor.GetCurrentScene().Sequences[0].End).Timestamp;

		// 				for(int n=0; n<frameSplits.Length-1; n++){
		// 					//Generating
		// 					List<State> states = new List<State>();
		// 					float start = Editor.GetCurrentFile().Data.GetFrame(frameSplits[n].FrameNumber).Timestamp;
		// 					float end = Editor.GetCurrentFile().Data.GetFrame(frameSplits[n+1].FrameNumber).Timestamp;
		// 					for(float t=start; t<=end; t+=1f/Framerate) {
		// 						Editor.LoadFrame(t);
		// 						states.Add(new State(Editor));

		// 						Generating = (t-start) / (end-start);

		// 						items += 1;
		// 						if(items == BatchSize) {
		// 							items = 0;
		// 							yield return new WaitForSeconds(0f);
		// 						}
		// 					}

		// 					//Precomputations
		// 					string[] styles = GetActiveStyles();
		// 					int[] stylemapping = GetMapping(states[0].Trajectory.Styles, styles);
		// 					string[] gaits = GetActiveGaits();
		// 					int[] gaitmapping = GetMapping(states[0].Trajectory.Gaits, gaits);
		// 					Data X; // Empty variables used for easier code reading
		// 					Data Y;

		// 					if(current_gait == "TR" || current_gait == "TR1" || current_gait == "TR2" || current_gait == "TR3" || current_gait == "TR4"){
		// 						if(SplitTransitions && frameSplits[n].Name == "valStartFrame"){
		// 							X = valX;
		// 							Y = valY;
		// 						}
		// 						else if(SplitTransitions && frameSplits[n].Name == "testStartFrame"){
		// 							X = testX;
		// 							Y = testY;
		// 						}
		// 						else{
		// 							X = trainX;
		// 							Y = trainY;
		// 						}
		// 					}
		// 					else{
		// 						if(frameSplits[n].Name == "valStartFrame"){
		// 							X = valX;
		// 							Y = valY;
		// 						}
		// 						else if(frameSplits[n].Name == "testStartFrame"){
		// 							X = testX;
		// 							Y = testY;
		// 						}
		// 						else{
		// 							X = trainX;
		// 							Y = trainY;
		// 						}
		// 					}
							

		// 					//Writing
		// 					for(int j=1; j<states.Count-1; j++) {
		// 						State previous = states[j-1];
		// 						State current = states[j];
		// 						State next = states[j+1];

		// 						//Input
		// 						for(int k=0; k<12; k++) {
		// 							X.FeedXZ(current.Trajectory.Points[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Position");
		// 							X.FeedXZ(current.Trajectory.Points[k].GetDirection().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Direction");
		// 							X.FeedXZ(current.Trajectory.Points[k].GetVelocity().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Velocity");
		// 							// X.Feed(Filter(current.Trajectory.Points[k].Styles, stylemapping, styles.Length), Data.ID.Standard, "Trajectory"+(k+1)+"State"); // style one hot
		// 							// X.Feed(Filter(current.Trajectory.Points[k].Signals, stylemapping, styles.Length), Data.ID.Standard, "Trajectory"+(k+1)+"Signal");  // signale - for goal oriented
		// 						}
		// 						for(int k=0; k<current.Posture.Length; k++) {
		// 							X.Feed(current.Posture[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Position");
		// 							X.Feed(current.Posture[k].GetForward().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Forward");
		// 							X.Feed(current.Posture[k].GetUp().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Up");
		// 							X.Feed(current.Velocities[k].GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Velocity");
		// 						}
		// 						//X.Feed(current.HeightMap.GetIntensities(1f), Data.ID.Standard, "HeightMap");
		// 						if(StylePhase){									
		// 							for(int k=0; k<12; k++) {
		// 								Vector2 phaseVector = Utility.PhaseVector(current.Trajectory.Points[k].Phase);
		// 								float[] filteredStyles = Filter(current.Trajectory.Points[k].Styles, stylemapping, styles.Length);
		// 								for(int style=0; style<styles.Length; style++) {
		// 									X.Feed(filteredStyles[style] * phaseVector.x, Data.ID.Standard, "StylePhase"+(k+1)+"-"+style+"X");
		// 									X.Feed(filteredStyles[style] * phaseVector.y, Data.ID.Standard, "StylePhase"+(k+1)+"-"+style+"Y");
		// 								}
		// 							}
		// 						}
		// 						if(!StylePhase){
		// 							for(int k=0; k<12; k++) {
		// 								X.Feed(current.Trajectory.Points[k].Phase, Data.ID.Standard, "Phase"+(k+1));
		// 								float[] filteredStyles = Filter(current.Trajectory.Points[k].Styles, stylemapping, styles.Length);
		// 								for(int style=0; style<styles.Length; style++) {
		// 									X.Feed(filteredStyles[style], Data.ID.Standard, "StyleOneHot"+(k+1)+"-"+style);
		// 								}
		// 							}
		// 						}
		// 						if(GaitPhase){									
		// 							for(int k=0; k<12; k++) {
		// 								Vector2 phaseVector = Utility.PhaseVector(current.Trajectory.Points[k].Phase);
		// 								float[] filteredGaits = Filter(current.Trajectory.Points[k].Gaits, gaitmapping, gaits.Length);
		// 								for(int gait=0; gait<gaits.Length; gait++) {
		// 									X.Feed(filteredGaits[gait] * phaseVector.x, Data.ID.Standard, "GaitPhase"+(k+1)+"-"+gait+"X");
		// 									X.Feed(filteredGaits[gait] * phaseVector.y, Data.ID.Standard, "GaitPhase"+(k+1)+"-"+gait+"Y");
		// 								}
		// 							}
		// 						}
		// 						if(!GaitPhase){
		// 							for(int k=0; k<12; k++) {
		// 								// X.Feed(current.Trajectory.Points[k].Phase, Data.ID.Standard, "Phase"+(k+1));
		// 								float[] filteredGaits = Filter(current.Trajectory.Points[k].Gaits, gaitmapping, gaits.Length);
		// 								for(int gait=0; gait<gaits.Length; gait++) {
		// 									X.Feed(filteredGaits[gait], Data.ID.Standard, "GaitOneHot"+(k+1)+"-"+gait);
		// 								}
		// 							}
		// 						}
		// 						X.Store();
		// 						//

		// 						//Output
		// 						for(int k=6; k<12; k++) {
		// 							Y.FeedXZ(next.Trajectory.Points[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Position");
		// 							Y.FeedXZ(next.Trajectory.Points[k].GetDirection().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Direction");
		// 							Y.FeedXZ(next.Trajectory.Points[k].GetVelocity().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Trajectory"+(k+1)+"Velocity");
		// 							// Y.Feed(Filter(next.Trajectory.Points[k].Styles, stylemapping, styles.Length), Data.ID.Standard, "Trajectory"+(k+1)+"State");
		// 							//Y.Feed(Filter(ref next.Trajectory.Points[k].Styles, ref next.Trajectory.Styles, ref Styles), Data.ID.Standard, "Trajectory"+(k+1)+"State");
		// 						}
		// 						for(int k=0; k<next.Posture.Length; k++) {
		// 							Y.Feed(next.Posture[k].GetPosition().GetRelativePositionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Position");
		// 							Y.Feed(next.Posture[k].GetForward().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Forward");
		// 							Y.Feed(next.Posture[k].GetUp().GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Up");
		// 							Y.Feed(next.Velocities[k].GetRelativeDirectionTo(current.Root), Data.ID.Standard, "Bone"+(k+1)+"Velocity");
		// 						}
		// 						for(int k=6; k<12; k++) {
		// 							Y.Feed(Utility.PhaseUpdate(current.Trajectory.Points[k].Phase, next.Trajectory.Points[k].Phase), Data.ID.Standard, "PhaseUpdate"+(k+1));
		// 						}
		// 						Y.Store();
		// 						//

		// 						Writing = (float)j / (float)(states.Count-1);

		// 						items += 1;
		// 						if(items == BatchSize) {
		// 							items = 0;
		// 							yield return new WaitForSeconds(0f);
		// 						}
		// 					}
		// 				}
		// 			}
		// 		}
		// 	}

		// 	trainX.Finish();
		// 	trainY.Finish();
		// 	valX.Finish();
		// 	valY.Finish();
		// 	testX.Finish();
		// 	testY.Finish();

		// 	Exporting = false;
		// 	yield return new WaitForSeconds(0f);
		// }
	}
	

	private IEnumerator ExportDataSeparately() {
		if(Editor == null) {
			Debug.Log("No editor found.");
		} else if(TestSplit+ValSplit+TrainSplit != 100f){
			Debug.Log("Data splits do not sum to 100");
		} else {
			Debug.Log("Separate Exporting not yet implemented");
			yield return new WaitForSeconds(0f);
		}
	}
	
	public float GetWeight(float x) {
		return Mathf.Exp(-Mathf.Pow(x-6, 2) / 12f);
		//return Mathf.Pow((1f / (1f + Mathf.Exp(-(x-6f)))), 0.5f);
		//return 1f - Mathf.Pow((1f / (1f + Mathf.Exp(-(x-6f)))), 2f);
	}

	private string[] GetActiveStyles() {
		List<string> styles = new List<string>();
		for(int i=0; i<StyleNames.Count; i++) {
			if(StyleExport[i]) {
				styles.Add(StyleNames[i]);
			}
		}
		return styles.ToArray();
	}

	private string[] GetActiveGaits() {
		List<string> gaits = new List<string>();
		for(int i=0; i<GaitNames.Count; i++) {
			if(GaitExport[i]) {
				gaits.Add(GaitNames[i]);
			}
		}
		return gaits.ToArray();
	}

	private int[] GetMapping(string[] from, string[] to) {
		int[] mapping = new int[from.Length];
		for(int i=0; i<from.Length; i++) {
			mapping[i] = -1;
			for(int j=0; j<to.Length; j++) {
				if(from[i] == to[j]) {
					mapping[i] = j;
				}
			}
		}
		return mapping;
	}

	private float[] Filter(float[] values, int[] mapping, int dimensionality) {
		if(values.Length != mapping.Length) {
			Debug.Log("Dimensionality of values and mapping does not match.");
			return values;
		}
		float[] result = new float[dimensionality];
		for(int i=0; i<mapping.Length; i++) {
			if(mapping[i] != -1) {
				result[mapping[i]] = values[i];
			}
		}
		return result;
	}

	/*
	private float[] Filter(ref float[] values, ref string[] from, ref string[] to) {
		float[] filtered = new float[to.Length];
		for(int i=0; i<to.Length; i++) {
			for(int j=0; j<from.Length; j++) {
				if(to[i] == from[j]) {
					filtered[i] = values[j];
				}
			}
		}
		return filtered;
	}
	*/

	public class Data {
		public StreamWriter File, Norm, Labels;
		public enum ID {Standard, Ignore, IgnoreMean, IgnoreStd}

		public RunningStatistics[] Mean = null;
		public RunningStatistics[] Std = null;


		private float[] Values = new float[0];
		private ID[] Types = new ID[0];
		private string[] Names = new string[0];
		private float[] Weights = new float[0];
		private int Dim = 0;

		public Data(StreamWriter file, StreamWriter norm, StreamWriter labels) {
			File = file;
			Norm = norm;
			Labels = labels;
		}

		public void Feed(float value, ID type, string name, float weight=1f) {
			Dim += 1;
			if(Values.Length < Dim) {
				ArrayExtensions.Add(ref Values, value);
			} else {
				Values[Dim-1] = value;
			}
			if(Types.Length < Dim) {
				ArrayExtensions.Add(ref Types, type);
			}
			if(Names.Length < Dim) {
				ArrayExtensions.Add(ref Names, name);
			}
			if(Weights.Length < Dim) {
				ArrayExtensions.Add(ref Weights, weight);
			}
		}

		public void Feed(float[] values, ID type, string name, float weight=1f) {
			for(int i=0; i<values.Length; i++) {
				Feed(values[i], type, name + (i+1), weight);
			}
		}

		public void Feed(Vector3 value, ID type, string name, float weight=1f) {
			Feed(value.x, type, name+"X", weight);
			Feed(value.y, type, name+"Y", weight);
			Feed(value.z, type, name+"Z", weight);
		}

		public void FeedXY(Vector3 value, ID type, string name, float weight=1f) {
			Feed(value.x, type, name+"X", weight);
			Feed(value.y, type, name+"Y", weight);
		}

		public void FeedXZ(Vector3 value, ID type, string name, float weight=1f) {
			Feed(value.x, type, name+"X", weight);
			Feed(value.z, type, name+"Z", weight);
		}

		public void FeedYZ(Vector3 value, ID type, string name, float weight=1f) {
			Feed(value.y, type, name+"Y", weight);
			Feed(value.z, type, name+"Z", weight);
		}

		public void Feed(Quaternion value, ID type, string name, float weight=1f) {
			Feed(value.x, type, name+"X", weight);
			Feed(value.y, type, name+"Y", weight);
			Feed(value.z, type, name+"Z", weight);
			Feed(value.w, type, name+"W", weight);
		}

		public void Store() {
			if(Norm != null) {
				if(Mean == null && Std == null) {
					Mean = new RunningStatistics[Values.Length];
					for(int i=0; i<Mean.Length; i++) {
						Mean[i] = new RunningStatistics();
					}
					Std = new RunningStatistics[Values.Length];
					for(int i=0; i<Std.Length; i++) {
						Std[i] = new RunningStatistics();
					}
				}
				for(int i=0; i<Values.Length; i++) {
					switch(Types[i]) {
						case ID.Standard:		//Ground Truth
						Mean[i].Add(Values[i]);
						Std[i].Add(Values[i]);
						break;
						case ID.Ignore:			//Mean 0.0 Std 1.0
						Mean[i].Add(0f);
						Std[i].Add(-1f);
						Std[i].Add(1f);
						break;
						case ID.IgnoreMean:		//Mean 0.0 Std GT
						Mean[i].Add(0f);
						Std[i].Add(Values[i]);
						break;
						case ID.IgnoreStd:		//Mean GT Std 1.0
						Mean[i].Add(Values[i]);
						Std[i].Add(-1f);
						Std[i].Add(1f);
						break;
					}
				}
			}

			if(File != null) {
				string[] elements = new string[Values.Length];
				for(int i=0; i<elements.Length; i++) {
					elements[i] = Values[i].ToString(Accuracy);
				}
				string line = System.String.Join(Separator, elements);
				line = line.Replace(",",".");
				File.WriteLine(line);
			}

			Dim = 0;
		}

		public void Finish() {
			if(Labels != null) {
				for(int i=0; i<Names.Length; i++) {
					Labels.WriteLine("[" + i + "]" + " " + Names[i]);
				}
				Labels.Close();
			}

			if(File != null) {
				File.Close();
			}

			if(Norm != null) {
				string mean = string.Empty;
				for(int i=0; i<Mean.Length; i++) {
					mean += Mean[i].Mean().ToString(Accuracy) + Separator;
				}
				mean = mean.Remove(mean.Length-1);
				mean = mean.Replace(",",".");
				Norm.WriteLine(mean);

				string std = string.Empty;
				for(int i=0; i<Std.Length; i++) {
					std += (Std[i].Std() / Weights[i]).ToString(Accuracy) + Separator;
				}
				std = std.Remove(std.Length-1);
				std = std.Replace(",",".");
				Norm.WriteLine(std);

				Norm.Close();

				Mean = null; // added for ProcSeparate so can calculate new mean and std for each style
				Std = null;
				
			}
		}
	}

	public class State {
		public Matrix4x4 Root;
		public Matrix4x4[] Posture;
		public Vector3[] Velocities;
		public Trajectory Trajectory;
		//public HeightMap HeightMap;
		public float[] FootContacts;

		public State(MotionEditor editor) {
			MotionFile file = editor.GetCurrentFile();
			Frame frame = editor.GetCurrentFrame();

			Posture = editor.GetActor().GetPosture();
			Velocities = editor.GetActor().GetVelocities();
			Trajectory = ((TrajectoryModule)file.Data.GetModule(Module.TYPE.Trajectory)).GetTrajectory(frame, editor.Mirror);
			Root = Trajectory.Points[6].GetTransformation();
			//HeightMap = ((HeightMapModule)file.Data.GetModule(Module.TYPE.HeightMap)).GetHeightMap(editor.GetActor());
			FootContacts = ((ContactModule)file.Data.GetModule(Module.TYPE.Contact)).GetFootContacts(frame, editor.Mirror);
		}
	}

	public class FrameSplit : IComparable{
		public string Name;
		public int FrameNumber;

		public FrameSplit(string name, int framenumber){
			Name = name;
			FrameNumber = framenumber;
		}

		int IComparable.CompareTo(object obj){
			FrameSplit a=(FrameSplit)obj;
			return this.FrameNumber.CompareTo(a.FrameNumber);
		}
	}

}
#endif
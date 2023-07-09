using UnityEngine;

[System.Serializable]
public class Sequence {
	public int Start;
	public int End;

	public Sequence(int start, int end) {
		SetStart(start);
		SetEnd(end);
	}

	public int GetLength() {
		return End - Start + 1;
	}

	public void SetStart(int index) {
		Start = index;
	}

	public void SetEnd(int index) {
		End = index;
	}

	public bool Contains(int index) {
		return index >= Start && index <= End;
	}
}

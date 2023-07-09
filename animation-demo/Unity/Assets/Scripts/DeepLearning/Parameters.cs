using System.IO;
using UnityEngine;

namespace DeepLearning {

    public class Parameters : ScriptableObject {
        public Buffer[] Buffers = new Buffer[0];

        public void Store(string fn, int size, string id) {
            for(int i=0; i<Buffers.Length; i++) {
                if(Buffers[i] != null) {
                    if(Buffers[i].ID == id) {
                        Debug.Log("Buffer with ID " + id + " already contained.");
                        return;
                    }
                }
            }
            ArrayExtensions.Add(ref Buffers, ReadBinary(fn, size, id));
        }

        public Buffer Load(string id) {
            Buffer buffer = System.Array.Find(Buffers, x => x.ID == id);
            if(buffer == null) {
                Debug.Log("Buffer with ID " + id + " not found.");
            }
            return buffer;
        }

        public void Clear() {
            ArrayExtensions.Resize(ref Buffers, 0);
        }

        private Buffer ReadBinary(string fn, int size, string id) {
            if(File.Exists(fn)) {
                Buffer buffer = new Buffer(id, size);
                BinaryReader reader = new BinaryReader(File.Open(fn, FileMode.Open));
                int errors = 0;
                for(int i=0; i<size; i++) {
                    try {
                        buffer.Values[i] = reader.ReadSingle();
                    } catch {
                        errors += 1;
                    }
                }
                reader.Close();
                if(errors > 0) {
                    Debug.Log("There were " + errors + " errors reading file at path " + fn + ".");
                    return null;
                } else {
                    return buffer;
                }
            } else {
                Debug.Log("File at path " + fn + " does not exist.");
                return null;
            }
        }

        public bool Validate() {
            for(int i=0; i<Buffers.Length; i++) {
                if(Buffers[i] == null) {
                    return false;
                }
            }
            return true;
        }

        [System.Serializable]
        public class Buffer {
            public string ID;
            public float[] Values;
            public Buffer(string id, int size) {
                ID = id;
                Values = new float[size];
            }
        }
    }
	
}
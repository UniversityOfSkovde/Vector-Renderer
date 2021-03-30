# Vector Renderer
With this package, you can draw vectors in Unity that works both in Edit and 
Play Mode.

## Installation
You can add this package to your project by going to 
`Window -> Package Manager -> + -> Add Package From git URL...` 
and then enter `https://github.com/UniversityOfSkovde/Vector-Renderer.git`.

## Usage
Create an empty GameObject and add a `VectorRenderer`-component to it. 
Next, write a custom script and attach it to the same object and access 
it like this:

```csharp
using System;
using UnityEditor;
using UnityEngine;
using Vectors;

[ExecuteAlways]
[RequireComponent(typeof(VectorRenderer))]
public class Example : MonoBehaviour {
    
    [NonSerialized] 
    private VectorRenderer vectors;

    [SerializeField]
    public Vector3 vectorA = new Vector3(3, 0, 0);
    
    [SerializeField]
    public Vector3 vectorB = new Vector3(0, 3, 0);
    
    [SerializeField]
    public Vector3 vectorC = new Vector3(0, 0, 3);
    
    void OnEnable() {
        vectors = GetComponent<VectorRenderer>();
    }

    void Update()
    {
        using (vectors.Begin()) {
            vectors.Draw(Vector3.zero, vectorA, Color.red);
            vectors.Draw(Vector3.zero, vectorB, Color.green);
            vectors.Draw(Vector3.zero, vectorC, Color.blue);
        }
    }
}

[CustomEditor(typeof(Example))]
public class ExampleGUI : Editor {
    void OnSceneGUI() {
        var ex = target as Example;
        if (ex == null) return;

        EditorGUI.BeginChangeCheck();
        var a = Handles.PositionHandle(ex.vectorA, Quaternion.identity);
        var b = Handles.PositionHandle(ex.vectorB, Quaternion.identity);
        var c = Handles.PositionHandle(ex.vectorC, Quaternion.identity);

        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(target, "Vector Positions");
            ex.vectorA = a;
            ex.vectorB = b;
            ex.vectorC = c;
            EditorUtility.SetDirty(target);
        }
    }
}
```

All vectors drawn between `.Begin()` and `.End()` will be in the same draw-call.

## License
Copyright 2020 Emil Forslund

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the "Software"), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished 
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all 
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS 
IN THE SOFTWARE.
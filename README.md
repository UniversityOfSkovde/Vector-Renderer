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
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(VectorRenderer))]
public class Example : MonoBehaviour {
    
    [NonSerialized] 
    private VectorRenderer vectors;
    
    void OnEnable() {
        vectors = GetComponent<VectorRenderer>();
    }

    void Update()
    {
        vectors.Begin();
        
        vectors.Draw(new Vector3(0, 0, 0), new Vector3(4, 0, 0), Color.red);
        vectors.Draw(new Vector3(0, 0, 0), new Vector3(0, 4, 0), Color.green);
        vectors.Draw(new Vector3(0, 0, 0), new Vector3(0, 0, 4), Color.blue);
        
        vectors.End();
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
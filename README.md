# GameDevToolingTestAssignment

## Usage

To run the tool, use the following command:

```bash
./MySolution.exe <unity_project_path> <output_folder_path>
```

### The source file is `UnityProjectAnalyzer.cs`


## Output on Task1:

SampleScene.unity.dump 
```
Cylinder
Directional Light
Main Camera
Plane
Cube
```

SecondScene.unity.dump 
```
Main Camera
Parent
--Child2
--Child1
----ChildNested
Directional Light
```

UnusedScripts.txt
```
Relative Path,GUID
Assets/Scripts/UnusedScript.cs,0111ada5c04694881b4ea1c5adfed99f
Assets/Scripts/Nested/UnusedScript2.cs,4851f847002ac48c487adaab15c4350c
```

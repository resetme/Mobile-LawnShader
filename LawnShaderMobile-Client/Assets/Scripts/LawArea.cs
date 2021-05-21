using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEngine.Profiling;
using UnityEditor;
#endif

//OLD VERSION NO MESH API AND UNITY JOBS

[ExecuteInEditMode]
public class LawArea : MonoBehaviour
{
    public bool showDebug = false;
    public bool updateMesh = false;
    
    public GameObject field;
    public Transform observerPosition;
    public float observerOffsetPosition = 0;
    public Mesh spawnMesh;
    public Material material;

    [Header("Lawn Size")]
    public int lawnWidth;
    public int lawnDepth;

    public int widthDivision = 0;
    public int depthDivition = 0;

    [Range(1,50)]
    public int subDivision = 4;

    [Header("Lawn Cull Properties")]
    [Range(0,100)]
    public float viewDistance = 1;
    [Range(-1f,1f)]
    public float minAngle = 0;
    [Range(-1f,1f)]
    public float maxAngle = 0;

    [Header("Lawn Cascade Properties")] 
    [Range(0, 1)]
    public float _cascade1multiplier = 1;
    [Range(0, 1)]
    public float _cascade2multiplier = 1;
    [Range(0, 1)]
    public float _cascade3multiplier = 1;
    
    [Header("Lawn Properties")]
    [Range(0,5000)]
    public int lawnAmount = 100;
    [Range(0.01f,5f)]
    public float lawnHeight = 1f;
    [Range(0.1f,1f)]
    public float lawnRandomHeight = 1f;
    [Range(0.1f, 10f)] 
    public float lawnScale = 1;

    [Header("Lawn Color")]
    [Range(0,10)]
    public float lawnIntensity;
    public Texture2D lawnTexture;
    public Color lawnColorGround;

    public DRAWTYPE drawType;

    [Header("Cube Shadow")]
    public Texture shadowMap;
    public Vector4 cubeMapUv;
    [Range(0.0f,1.0f)]
    public float shadowIntensity;
    
    [Header("Cloud Shadow")]
    public Texture cloudMap;
    public Vector4 cloudUV1;
    public Vector4 cloudUV2;
    [Range(0.0f,1.0f)]
    public float cloudIntensity1;
   
    [Range(0.0f,1.0f)]
    public float cloudIntensity2;

    [Header("Snow Texture")] 
    public Texture snowShadow;
    
    [Header("Top Color")] 
    [ColorUsageAttribute(true,true,0f,8f,0.125f,3f)]public Color topColor;
    [Range(-1.0f,1.0f)]
    public float topColorLevel;

    private int _shader_MainTex = Shader.PropertyToID("_MainTex");
    private int _shader_MainIntensity = Shader.PropertyToID("_MainIntensity");
    private int _shader_ColorGround = Shader.PropertyToID("_ColorGround");
    
    //Cube Shadow
    private int _shader_CubeShadow = Shader.PropertyToID("_CubeShadow");
    private int _shader_CubeMapUV = Shader.PropertyToID("_CubeMapUV");
    private int _shader_CubeShadowIntensity = Shader.PropertyToID("_CubeShadowIntensity");
    
    //Cloud Shadow
    private int _shader_CloudShadow = Shader.PropertyToID("_CloudShadow");
    private int _shader_CloudShadowUV1 = Shader.PropertyToID("_CloudShadowUV1");
    private int _shader_CloudShadowIntensity1 = Shader.PropertyToID("_CloudShadowIntensity1");
    private int _shader_CloudShadowUV2 = Shader.PropertyToID("_CloudShadowUV2");
    private int _shader_CloudShadowIntensity2 = Shader.PropertyToID("_CloudShadowIntensity2");
    
    //Top Color
    private int _shader_TopColor = Shader.PropertyToID("_TopColor");
    private int _shader_TopColorLevel = Shader.PropertyToID("_TopColorLevel");
    
    //Snow
    private int _shader_SnowShadow = Shader.PropertyToID("_SnowShadow");

    public enum DRAWTYPE
    {
        cascade, fadeIn, cascadeAndFade
    }
    
    private struct AreaInfo
    {
        public Vector3 position;
        public Vector3 scale;
        public bool visible;
        public SubAreaInfo[] subAreaInfos;
    }

    private struct SubAreaInfo
    {
        public Vector3 position;
        public Vector3 scale;
        public float dotValue;
        public bool visible;
        public Matrix4x4 trs;
        public int cascadeValue;
    }
    
    private struct MeshList
    { 
        public bool draw;
        public Mesh mesh;
        public List<Matrix4x4> positionList; 
    }

    private bool _initialize = false;
    private Mesh _createdMesh;
    private readonly Mesh[] _cascadeMesh = new Mesh[4];
    private AreaInfo[] _areaInfos;
    private MeshList[] _cascadeMeshList;
    private LayerMask _lawnLayer;
    MaterialPropertyBlock _block;
    private Material _fieldMaterial;
    
    void Start()
    {
        InitialSetup();
    }

    private void Update()
    {
        //DO GPU Instancing here
        //Check Distance and Angle
        if (!_initialize)
            return;
        
        Vector3 targetObserverPosition = observerPosition.transform.position ;
        targetObserverPosition += observerPosition.transform.forward * observerOffsetPosition;
        
        for (int i = 0; i < _areaInfos.Length; i++)
        {
            float distance = Vector3.Distance(targetObserverPosition, _areaInfos[i].position);
            
            if (distance < viewDistance)
            {
                _areaInfos[i].visible = true;
                
                //SubDivision Check
                for (int j = 0; j < _areaInfos[i].subAreaInfos.Length; j++)
                {
                    distance = Vector3.Distance(targetObserverPosition, _areaInfos[i].subAreaInfos[j].position);
                    if (distance < viewDistance)
                    {
                        Vector3 up = (_areaInfos[i].subAreaInfos[j].position - targetObserverPosition).normalized;
                        Vector3 forward = transform.TransformDirection(observerPosition.transform.forward);
                        float dot = Vector3.Dot(forward, up);
                        //dot = Mathf.Clamp01(dot);
                    
                        _areaInfos[i].subAreaInfos[j].dotValue = dot;
                    
                        if (dot > minAngle && dot < maxAngle)
                        {
                            _areaInfos[i].subAreaInfos[j].visible = true;
                            
                            float normalizeDistance = distance / viewDistance;
                    
                            switch (drawType)
                            {
                                case DRAWTYPE.cascade:
                                    _areaInfos[i].subAreaInfos[j].cascadeValue = SelectCascadeMesh(normalizeDistance);
                                    break;
                                case DRAWTYPE.fadeIn:
                                    _areaInfos[i].subAreaInfos[j].position.y = -Mathf.Lerp(-0.2f, 1.2f * (lawnHeight/2f), Mathf.SmoothStep(0.2f,1f, normalizeDistance));
                                    _areaInfos[i].subAreaInfos[j].cascadeValue = 1;
                                    break;
                                case DRAWTYPE.cascadeAndFade:
                                    _areaInfos[i].subAreaInfos[j].cascadeValue = SelectCascadeMesh(normalizeDistance);
                                    float heightMultiplier = Mathf.Max(0.2f, _areaInfos[i].subAreaInfos[j].cascadeValue/(float)(_cascadeMesh.Length-1));
                                    _areaInfos[i].subAreaInfos[j].position.y = -Mathf.Lerp(-0.2f, 1.2f * (lawnHeight/2f), Mathf.SmoothStep(0.2f,1f, normalizeDistance));

                                    break;
                            }
                            _areaInfos[i].subAreaInfos[j].trs.SetTRS(_areaInfos[i].subAreaInfos[j].position, Quaternion.identity, Vector3.one);
                        }
                        else
                        {
                            _areaInfos[i].subAreaInfos[j].visible = false;
                        }
                    }
                    else
                    {
                        _areaInfos[i].subAreaInfos[j].visible = false;
                    }
                } 
            }
            else
            {
                _areaInfos[i].visible = false;
            }
        }
        
        //Draw Mesh
        DrawMesh();
    }

    private int SelectCascadeMesh(float distance)
    {
        int index = Mathf.CeilToInt(distance * _cascadeMesh.Length);
        return index;
    }

    private void ShaderUpdateProperties()
    {
        material.SetTexture(_shader_MainTex, lawnTexture);
        material.SetFloat(_shader_MainIntensity, lawnIntensity);
        material.SetColor(_shader_ColorGround, lawnColorGround);
        
        _fieldMaterial.SetTexture(_shader_MainTex, lawnTexture);
        _fieldMaterial.SetFloat(_shader_MainIntensity, lawnIntensity);
        _fieldMaterial.SetColor(_shader_ColorGround, lawnColorGround);

        //Cube Shadow
        Shader.SetGlobalVector(_shader_CubeMapUV, cubeMapUv);
        Shader.SetGlobalTexture(_shader_CubeShadow, shadowMap);
        Shader.SetGlobalFloat(_shader_CubeShadowIntensity, shadowIntensity);
        
        //Cloud Shadow
        Shader.SetGlobalTexture(_shader_CloudShadow, cloudMap);
        Shader.SetGlobalVector(_shader_CloudShadowUV1, cloudUV1);
        Shader.SetGlobalFloat(_shader_CloudShadowIntensity1, cloudIntensity1);
        Shader.SetGlobalVector(_shader_CloudShadowUV2, cloudUV2);
        Shader.SetGlobalFloat(_shader_CloudShadowIntensity2, cloudIntensity2);
        
        //Top Color
        Shader.SetGlobalVector(_shader_TopColor, topColor);
        Shader.SetGlobalFloat(_shader_TopColorLevel, topColorLevel);
        
        //Snow
        Shader.SetGlobalTexture(_shader_SnowShadow, snowShadow);

        material.mainTextureOffset = _fieldMaterial.mainTextureOffset;
        material.mainTextureScale = _fieldMaterial.mainTextureScale;
        

    }

    private void DrawMesh()
    {        
        for (int i = 0; i < _cascadeMeshList.Length; i++)
        {
            _cascadeMeshList[i].positionList.Clear();
        }
        
        for (int i = 0; i < _areaInfos.Length; i++)
        {
            if (_areaInfos[i].visible)
            {
                for (int j = 0; j < _areaInfos[i].subAreaInfos.Length; j++)
                {
                    if (_areaInfos[i].subAreaInfos[j].visible)
                    {
                        int value = _areaInfos[i].subAreaInfos[j].cascadeValue -1;
                        _cascadeMeshList[value].mesh = _cascadeMesh[(value)];
                        _cascadeMeshList[value].positionList.Add(_areaInfos[i].subAreaInfos[j].trs);
                    }
                }
            }
        }

        for (int i = 0; i < _cascadeMeshList.Length; i++)
        {
            if (_cascadeMeshList[i].positionList.Count > 1023)
            {
                Debug.Log("Cant draw more LIMIT");
                return;
            }
        }

        for (int i = 0; i < _cascadeMeshList.Length; i++)
        {
            if (_cascadeMeshList[i].positionList.Count == 0)
                continue;
            
            Graphics.DrawMeshInstanced(_cascadeMeshList[i].mesh, 0,  material, _cascadeMeshList[i].positionList, _block, ShadowCastingMode.Off, false, _lawnLayer);
        }
    }

    private void InitialSetup()
    {
        if (!CheckSetup())
        {
            _initialize = false;
            return;
        }
        
        _initialize = true;
        
        _cascadeMeshList = new MeshList[_cascadeMesh.Length];
        
        for (int i = 0; i < _cascadeMeshList.Length; i++)
        {
            _cascadeMeshList[i].positionList = new List<Matrix4x4>();
        }
        
        field.transform.localScale = new Vector3(lawnWidth, lawnDepth, 0);
        _fieldMaterial = field.GetComponent<MeshRenderer>().sharedMaterial;

        _lawnLayer = LayerMask.NameToLayer("Lawn");
        _block = new MaterialPropertyBlock();

        if (updateMesh)
        {
            //Create Field Area
            CreateAreaArrayInfo(ref _areaInfos, widthDivision, depthDivition, lawnWidth, lawnDepth, field.transform.position);
       
            _cascadeMesh[0] = CreateLawnArea(lawnAmount);
            _cascadeMesh[1] = CreateLawnAreaLod(_cascadeMesh[0], _cascade1multiplier, 1);
            _cascadeMesh[2] = CreateLawnAreaLod(_cascadeMesh[0], _cascade2multiplier, 1);
            _cascadeMesh[3] = CreateLawnAreaLod(_cascadeMesh[0], _cascade3multiplier, 1);  
        }

        ShaderUpdateProperties();
    }

    private bool CheckSetup()
    {
        bool ready = true;
        
        if (spawnMesh == null)
            ready = false;

        if (material == null)
            ready = false;

        if (lawnAmount == 0)
            ready = false;

        if (field == null)
            ready = false;

        if (observerPosition == null)
            ready = false;

        return ready;
    }

    private Mesh CreateLawnAreaLod(Mesh lawMesh, float multiplier, float heightMultiplier)
    {
        Mesh mesh = new Mesh();
        int reduce = (spawnMesh.vertices.Length * Mathf.FloorToInt(lawnAmount * multiplier));

        Vector3[] vertices = lawMesh.vertices;
        Array.Resize(ref vertices, reduce);
        mesh.vertices = vertices;
        
        int[] triangles = lawMesh.triangles;
        Array.Resize(ref triangles, reduce); //(reduce * 3) / 2 
        mesh.triangles = triangles;

        Vector2[] uvs = lawMesh.uv;
        Array.Resize(ref uvs, reduce);
        mesh.uv = uvs;
        
        /*
        Vector3[] normals = lawMesh.normals;
        Array.Resize(ref normals, reduce);
        mesh.normals = normals;
        */
        
        Color[] vertexColor = lawMesh.colors;
        Array.Resize(ref vertexColor, reduce);
        mesh.colors = vertexColor;
        return mesh;
    }
    
    private Mesh CreateLawnArea(int totalAmount)
    {
        //Create Mesh
        Mesh mesh = new Mesh();
        
        Mesh baseMesh = spawnMesh;
        int amount = totalAmount;
        
        Vector3[] vertices = new Vector3[baseMesh.vertices.Length * amount];
        Vector3[] baseVertices = baseMesh.vertices;
        int baseVerticesIndex = 0;
        
        //Random Height
        float randomHeight = Random.Range(lawnRandomHeight, 1f);
        
        //Area Position
        float randonXPosision = 0;
        float randonZPosision = 0;
        float width = lawnWidth / (float) depthDivition;
        float depth = lawnDepth / (float) widthDivision;
        width /= subDivision;
        depth /= subDivision;
       
        //Rotation
        float rotYRandom = Random.Range(-90f, 90f);

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = baseVertices[baseVerticesIndex];
            Vector3 vertexPosition = vertices[i];
            
            //Rotation
            vertexPosition = Quaternion.Euler(0, rotYRandom, 0) * vertexPosition;
            
            //Scale
            vertexPosition *= lawnScale;
            
            //Height
            vertexPosition.y *= lawnHeight;
            vertexPosition.y *= randomHeight;
            
            //Position
            vertexPosition.x += Vector3.zero.x + randonXPosision;
            vertexPosition.y += Vector3.zero.y;
            vertexPosition.z += Vector3.zero.z + randonZPosision;
            
            //Set
            vertices[i].x = vertexPosition.x;
            vertices[i].y = vertexPosition.y;
            vertices[i].z = vertexPosition.z;
            
            baseVerticesIndex++;

            if (baseVerticesIndex % baseMesh.vertices.Length == 0)
            {
                //Set new random position and rotation for next batch
                randomHeight = Random.Range(lawnRandomHeight, 1f);
                rotYRandom = Random.Range(-90f, 90f);
                randonXPosision = Random.Range(-width / 2f, width / 2f);
                randonZPosision = Random.Range(-depth / 2f, depth / 2f);  
                baseVerticesIndex = 0;
            }
        }

        mesh.vertices = vertices;
        
        int[] triangles = new int[baseMesh.triangles.Length * amount];
        int[] baseTriangles = baseMesh.triangles;
        int baseTriangleIndex = 0;

        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = baseTriangles[baseTriangleIndex];

            baseTriangleIndex++;
            if (baseTriangleIndex % baseTriangles.Length == 0)
            {
                baseTriangleIndex = 0;
                for (int j = 0; j < baseTriangles.Length; j++)
                {
                    baseTriangles[j] += baseMesh.vertexCount;
                }
            }
        }

        mesh.triangles = triangles;
        
        //UV
        Vector2[] uvs = new Vector2[baseMesh.vertices.Length * amount];
        Vector2[] baseUV = baseMesh.uv;
        int baseUvsIndex = 0;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = baseUV[baseUvsIndex];

            baseUvsIndex++;

            if (baseUvsIndex % baseMesh.vertices.Length == 0)
            {
                baseUvsIndex = 0;
            }
        }

        mesh.uv = uvs;

        //Normals
        /*
        Vector3[] normals = new Vector3[baseMesh.vertices.Length * amount];
        Vector3[] baseNormals = baseMesh.normals;
        int baseNormalsIndex = 0;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = baseNormals[baseNormalsIndex];

            baseNormalsIndex++;

            if (baseNormalsIndex % baseMesh.vertices.Length == 0)
            {
                baseNormalsIndex = 0;
            }
        }

        mesh.normals = normals;
        */
        
        //Vertex Color
        float redRandom = Random.Range(0.5f, 1f);
        float greenGrad;
        float blueRandom = Random.Range(0f, 1f);

        Color[] vertexColor = new Color[baseMesh.vertexCount * amount];
        Color[] baseColor = baseMesh.colors;
        int baseVertexColorIndex = 0;
        for (int i = 0; i < vertexColor.Length; i++)
        {
            vertexColor[i].r = redRandom;
            vertexColor[i].g = baseColor[baseVertexColorIndex].g;
            vertexColor[i].b = blueRandom;
            vertexColor[i].a = 1;

            baseVertexColorIndex++;

            if (baseVertexColorIndex % baseMesh.vertexCount == 0)
            {
                redRandom = Random.Range(0.5f, 1f);
                blueRandom = Random.Range(0f, 1f);
                baseVertexColorIndex = 0;
            }
        }

        mesh.colors = vertexColor;
        
        return mesh;
    }

    private void CreateAreaArrayInfo(ref AreaInfo[] areaInfos, int areaWidthDivision, int areaDepthDivision, float depth, float width, Vector3 areaPos)
    {       
        areaInfos = new AreaInfo[areaWidthDivision * areaDepthDivision];
        
        Vector3 initPos;
        var scaleXYZ = ScaleXyz(areaWidthDivision, areaDepthDivision, depth, width, areaPos, out initPos);
        
        int index = -1;
        int indexPosX = 0;
        for (int i = 0; i < areaInfos.Length; i++)
        {
            if (i % areaDepthDivision == 0)
            {
                indexPosX = 0;
                index++;
            }
            
            Vector3 pos = initPos;
            pos.x = pos.x - (scaleXYZ.x * indexPosX);
            pos.z = pos.z - (scaleXYZ.z * index);

            indexPosX++;

            areaInfos[i].position = pos;
            areaInfos[i].visible = false;
            areaInfos[i].scale = scaleXYZ;
            areaInfos[i].subAreaInfos = new SubAreaInfo[subDivision*subDivision];

            for (int j = 0; j < areaInfos[i].subAreaInfos.Length; j++)
            {
                CreateSubAreaArrayInfo(ref areaInfos[i].subAreaInfos, subDivision, subDivision, areaInfos[i].scale.x, areaInfos[i].scale.z, areaInfos[i].position);
            }
        }
    }

    private void CreateSubAreaArrayInfo(ref SubAreaInfo[] areaInfos, int areaWidthDivision, int areaDepthDivision, float depth, float width, Vector3 areaPos)
    {
        areaInfos = new SubAreaInfo[areaWidthDivision * areaDepthDivision];

        Vector3 initPos;
        var scaleXYZ = ScaleXyz(areaWidthDivision, areaDepthDivision, depth, width, areaPos, out initPos);


        int index = -1;
        int indexPosX = 0;
        for (int i = 0; i < areaInfos.Length; i++)
        {
            if (i % areaDepthDivision == 0)
            {
                indexPosX = 0;
                index++;
            }
            
            Vector3 pos = initPos;
            pos.x = pos.x - (scaleXYZ.x * indexPosX);
            pos.z = pos.z - (scaleXYZ.z * index);

            indexPosX++;

            areaInfos[i].position = pos;
            areaInfos[i].visible = false;
            areaInfos[i].scale = scaleXYZ;
            areaInfos[i].dotValue = 0;
            areaInfos[i].trs = Matrix4x4.identity;
        }
    }

    private Vector3 ScaleXyz(int areaWidthDivision, int areaDepthDivision, float depth, float width, Vector3 areaPos,
        out Vector3 initPos)
    {
        float scaleX = width / (float) areaWidthDivision;
        float scaleZ = depth / (float) areaDepthDivision;
        Vector3 scaleXYZ = new Vector3(scaleZ, 1, scaleX);

        float posX = areaPos.x + depth / 2f;
        posX -= scaleZ / 2f;
        float posY = 0;
        float posZ = areaPos.z + width / 2f;
        posZ -= scaleX / 2f;

        initPos = new Vector3(posX, 0, posZ);
        return scaleXYZ;
    }

#if UNITY_EDITOR
    float GetMeshMemorySizeMB(Mesh mesh)
    {
        return (UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh) / 1024f) / 1024f;
    }
    
    private void OnValidate()
    {
            InitialSetup();
    }

    private void OnDrawGizmos()
    {
        if (!_initialize)
            return;
        
        if (!showDebug)
            return;
        
        Gizmos.color = Color.white;
        
        for (int i = 0; i < depthDivition; i++)
        {
            Vector3 posStart = field.transform.position;
            float xPos = Mathf.Lerp(posStart.x + (lawnWidth / 2f), posStart.x - (lawnWidth / 2f), i/(float)depthDivition);
            posStart.x = xPos;
            posStart.z = posStart.z - (lawnDepth / 2f);
            
            Vector3 posEnd = field.transform.position;
            posEnd.x = xPos;
            posEnd.z = posEnd.z + (lawnDepth / 2f);
            Gizmos.DrawLine(posStart, posEnd);
        }

        for (int i = 0; i < widthDivision; i++)
        {
            Vector3 posStart = field.transform.position;
            float zPos = Mathf.Lerp(posStart.z + (lawnDepth / 2f), posStart.z - (lawnDepth / 2f), i/(float)widthDivision);
            posStart.z = zPos;
            posStart.x = posStart.x - (lawnWidth / 2f);
            
            Vector3 posEnd = field.transform.position;
            posEnd.z = zPos;
            posEnd.x = posEnd.x + (lawnWidth / 2f);
            Gizmos.DrawLine(posStart, posEnd);
        }
        
        Vector3 targetObserverPosition = observerPosition.transform.position ;
        targetObserverPosition += observerPosition.transform.forward * observerOffsetPosition;

        Gizmos.DrawRay(targetObserverPosition, observerPosition.transform.forward * 10);

        int visibleAmount1 = 0;
        int visibleAmount2 = 0;
        int visibleAmount3 = 0;
        int visibleAmount4 = 0;
        
        //Check Distance and Angle from Observer
        for (int i = 0; i < _areaInfos.Length; i++)
        {
            if (_areaInfos[i].visible)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_areaInfos[i].position, _areaInfos[i].scale);
                
                //SubDivision
                for (int j = 0; j < _areaInfos[i].subAreaInfos.Length; j++)
                {
                    if (_areaInfos[i].subAreaInfos[j].visible)
                    {
                        switch (_areaInfos[i].subAreaInfos[j].cascadeValue)
                        {
                            case 1:
                                Gizmos.color = Color.green;
                                visibleAmount1++;
                                break;
                            case 2:
                                Gizmos.color = Color.blue;
                                visibleAmount2++;
                                break;
                            case 3:
                                Gizmos.color = Color.yellow;
                                visibleAmount3++;
                                break;
                            case 4:
                                Gizmos.color = Color.red;
                                visibleAmount4++;
                                break;
                        }
                    
                        Handles.Label(_areaInfos[i].subAreaInfos[j].position, _areaInfos[i].subAreaInfos[j].cascadeValue.ToString());
                        Gizmos.DrawWireCube(_areaInfos[i].subAreaInfos[j].position, _areaInfos[i].subAreaInfos[j].scale); 
                    }
                }
            }
        }

        float memoryCascade = visibleAmount1 * GetMeshMemorySizeMB(_cascadeMesh[0]);
        memoryCascade += visibleAmount1 * GetMeshMemorySizeMB(_cascadeMesh[1]);
        memoryCascade += visibleAmount1 * GetMeshMemorySizeMB(_cascadeMesh[2]);
        memoryCascade += visibleAmount1 * GetMeshMemorySizeMB(_cascadeMesh[3]);

        Vector3 memoryLabelPosition = observerPosition.transform.position;
        memoryLabelPosition.y += 2;
        GUIStyle style = new GUIStyle();
        style.fontSize = 32;
        Handles.Label(memoryLabelPosition, memoryCascade.ToString(), style);
    }
    #endif
}

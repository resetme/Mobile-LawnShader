using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LawArea : MonoBehaviour
{
    public bool showDebug = false;
    
    public GameObject field;
    public Transform observerPosition;
    public Mesh spawnMesh;
    public Material material;

    [Header("Lawn Size")]
    public int lawnWidth;
    public int lawnDepth;

    public int widthDivision = 0;
    public int depthDivition = 0;

    [Header("Lawn Cull Properties")]
    [Range(0,100)]
    public float viewDistance = 1;
    [Range(-1f,1f)]
    public float minAngle = 0;
    [Range(-1f,1f)]
    public float maxAngle = 0;
    
    [Header("Lawn Properties")]
    [Range(0,5000)]
    public int lawnAmount = 100;
    [Range(0.1f,5f)]
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

    private int _shader_MainTex = Shader.PropertyToID("_MainTex");
    private int _shader_MainIntensity = Shader.PropertyToID("_MainIntensity");
    private int _shader_ColorGround = Shader.PropertyToID("_ColorGround");

    public enum DRAWTYPE
    {
        cascade, fadeIn, cascadeAndFade
    }
    
    private struct AreaInfo
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
        
        for (int i = 0; i < _areaInfos.Length; i++)
        {
            float distance = Vector3.Distance(observerPosition.transform.position, _areaInfos[i].position);
            
            if (distance < viewDistance)
            {
                Vector3 up = (_areaInfos[i].position - observerPosition.transform.position).normalized;
                Vector3 forward = transform.TransformDirection(observerPosition.transform.forward);
                float dot = Vector3.Dot(forward, up);
                dot = Mathf.Clamp01(dot);
                
                _areaInfos[i].dotValue = dot;
                
                if (dot > minAngle && dot < maxAngle)
                {
                    float normalizeDistance = distance / viewDistance;

                    _areaInfos[i].visible = true;
                    
                    switch (drawType)
                    {
                            case DRAWTYPE.cascade:
                                _areaInfos[i].cascadeValue = SelectCascadeMesh(normalizeDistance);
                                break;
                            case DRAWTYPE.fadeIn:
                                _areaInfos[i].position.y = -Mathf.Lerp(-0.2f, 1.2f * (lawnHeight/2f), Mathf.SmoothStep(0.2f,1f, normalizeDistance));
                                _areaInfos[i].cascadeValue = 1;
                                break;
                            case DRAWTYPE.cascadeAndFade:
                                _areaInfos[i].cascadeValue = SelectCascadeMesh(normalizeDistance);
                                float heightMultiplier = Mathf.Max(0.2f, _areaInfos[i].cascadeValue/(float)(_cascadeMesh.Length-1));
                                _areaInfos[i].position.y = -Mathf.Lerp(-0.2f, (heightMultiplier * lawnHeight)/2f, Mathf.SmoothStep(0.2f,1f, normalizeDistance));

                                break;
                    }
    
                    _areaInfos[i].trs.SetTRS(_areaInfos[i].position, Quaternion.identity, Vector3.one);
                }
                else
                {
                    _areaInfos[i].visible = false;
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
                int value = _areaInfos[i].cascadeValue -1;
                _cascadeMeshList[value].mesh = _cascadeMesh[(value)];
                _cascadeMeshList[value].positionList.Add(_areaInfos[i].trs);
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
        
        field.transform.localScale = new Vector3(lawnWidth, lawnDepth, 0);
        _fieldMaterial = field.GetComponent<MeshRenderer>().sharedMaterial;

        _cascadeMeshList = new MeshList[_cascadeMesh.Length];
        for (int i = 0; i < _cascadeMeshList.Length; i++)
        {
            _cascadeMeshList[i].positionList = new List<Matrix4x4>();
        }
        
        _lawnLayer = LayerMask.NameToLayer("Lawn");
        _block = new MaterialPropertyBlock();

        _cascadeMesh[0] = CreateLawnArea(lawnAmount);
        

        for (int i = 1; i < _cascadeMesh.Length; i++)
        {
            //Amount
            float multiplier = (i) /(float)_cascadeMesh.Length;
            multiplier = 1 - multiplier;
            
            //height
            float heightMultiplier = Mathf.Max(0.2f, i/(float)(_cascadeMesh.Length));
            heightMultiplier = 1 - heightMultiplier;
            heightMultiplier = 1;
            _cascadeMesh[i] = CreateLawnAreaLod(_cascadeMesh[0], multiplier, heightMultiplier);
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

        if (!ready)
            return ready;
        
        CreateAreaArrayInfo();
        
        if (_areaInfos.Length == 0)
            ready = false;

        return ready;
    }

    private Mesh CreateLawnAreaLod(Mesh lawMesh, float multiplier, float heightMultiplier)
    {
        Mesh mesh = new Mesh();
        Mesh baseMesh = lawMesh;
        int reduce = (spawnMesh.vertices.Length * Mathf.FloorToInt(lawnAmount * multiplier));

        
        //Vertices
        Vector3[] vertices = new Vector3[reduce];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertexPosition = baseMesh.vertices[i];
            vertexPosition.y *= heightMultiplier;
            
            //Set
            vertices[i].x = vertexPosition.x;
            vertices[i].y = vertexPosition.y;
            vertices[i].z = vertexPosition.z;
        }

        mesh.vertices = vertices;
        
        //Triangles
        int[] triangles = new int[(reduce * 3) / 2 ];
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = baseMesh.triangles[i];
        }

        mesh.triangles = triangles;
        
        //UVs
        Vector2[] uvs = new Vector2[reduce];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = baseMesh.uv[i];
        }

        mesh.uv = uvs;
        
        //Normal
        Vector3[] normals = new Vector3[reduce];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }

        mesh.normals = normals;
        
        //Color
        Color[] vertexColor = new Color[reduce];
        for (int i = 0; i < vertexColor.Length; i++)
        {
            vertexColor[i] = baseMesh.colors[i];
        }

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
        Vector3[] normals = new Vector3[baseMesh.vertices.Length * amount];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }

        mesh.normals = normals;
        
        //Vertex Color
        float redRandom = Random.Range(0.5f, 1f);

        Color[] vertexColor = new Color[baseMesh.vertexCount * amount];
        int baseVertexColorIndex = 0;
        for (int i = 0; i < vertexColor.Length; i++)
        {
            vertexColor[i].r = redRandom;
            vertexColor[i].g = 1;
            vertexColor[i].b = 1;
            vertexColor[i].a = 1;

            baseVertexColorIndex++;

            if (baseVertexColorIndex % baseMesh.vertexCount == 0)
            {
                redRandom = Random.Range(0.5f, 1f);
                baseVertexColorIndex = 0;
            }
        }

        mesh.colors = vertexColor;
        
        return mesh;
    }

    private void CreateAreaArrayInfo()
    {       
        _areaInfos = new AreaInfo[widthDivision * depthDivition];
        
        float scaleX = lawnDepth / (float) widthDivision;
        float scaleZ = lawnWidth / (float) depthDivition;
        Vector3 scaleXYZ = new Vector3(scaleZ, 1, scaleX);
        
        float posX = field.transform.position.x + lawnWidth/2f;
        posX -= scaleZ / 2f;
        float posY = 0;
        float posZ = field.transform.position.z + lawnDepth/2f;
        posZ -= scaleX / 2f;
        
        Vector3 initPos = new Vector3(posX, 0, posZ);
        
        int index = -1;
        int indexPosX = 0;
        for (int i = 0; i < depthDivition * widthDivision; i++)
        {
            if (i % depthDivition == 0)
            {
                indexPosX = 0;
                index++;
            }
            
            Vector3 pos = initPos;
            pos.x = pos.x - (scaleZ * indexPosX);
            pos.z = pos.z - (scaleX * index);

            indexPosX++;

            _areaInfos[i].position = pos;
            _areaInfos[i].visible = false;
            _areaInfos[i].scale = scaleXYZ;
            _areaInfos[i].dotValue = 0;
            _areaInfos[i].trs = Matrix4x4.identity;
        }
    }
    
#if UNITY_EDITOR
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
        
        Gizmos.color = Color.yellow;
        
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

        Gizmos.DrawRay(observerPosition.transform.position, observerPosition.transform.forward * 10);
        
        //Check Distance and Angle from Observer
        for (int i = 0; i < _areaInfos.Length; i++)
        {
            if (_areaInfos[i].visible)
            {
                //Handles.Label(_areaInfos[i].position, _areaInfos[i].dotValue.ToString());
                Handles.Label(_areaInfos[i].position, _areaInfos[i].cascadeValue.ToString());
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.red;
            }
            
            Gizmos.DrawWireCube(_areaInfos[i].position, _areaInfos[i].scale);
        }
    }
    #endif
}

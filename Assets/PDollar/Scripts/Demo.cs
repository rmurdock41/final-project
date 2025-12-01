using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using PDollarGestureRecognizer;
using Cinemachine;
using UnityEditor;

public class Demo : MonoBehaviour {

	private Vector3 loc;
	private GameManager gameManager;
	public LayerMask layerMask;

	public Transform gestureOnScreenPrefab;
	public Transform spherePrefab;
	public Transform jammoPrefab;
	public Transform sunPrefab;
    public Transform moonPrefab;
    public Transform plantPrefab;
    public GameObject inkParticlePrefab;

    private List<Gesture> trainingSet = new List<Gesture>();

	private List<Point> points = new List<Point>();
	private int strokeId = -1;

	private Vector3 virtualKeyPosition = Vector2.zero;
	private Rect drawArea;

	private RuntimePlatform platform;
	private int vertexCount = 0;

	private List<LineRenderer> gestureLinesRenderer = new List<LineRenderer>();
	private LineRenderer currentGestureLineRenderer;

	//GUI
	private string message;
	private bool recognized;
	private string newGestureName = "";

	void Start () {

		gameManager = FindObjectOfType<GameManager>();
		platform = Application.platform;
		drawArea = new Rect(0, 0, Screen.width, Screen.height);

		//Load pre-made gestures
		TextAsset[] gesturesXml = Resources.LoadAll<TextAsset>("GestureSet/10-stylus-MEDIUM/");
		foreach (TextAsset gestureXml in gesturesXml)
			trainingSet.Add(GestureIO.ReadGestureFromXML(gestureXml.text));

		//Load user custom gestures
		string[] filePaths = Directory.GetFiles(Application.persistentDataPath, "*.xml");
		foreach (string filePath in filePaths)
			trainingSet.Add(GestureIO.ReadGestureFromFile(filePath));
	}

	void Update () {

		if (!gameManager.isDrawing)
			return;

		if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer) {
			if (Input.touchCount > 0) {
				virtualKeyPosition = new Vector3(Input.GetTouch(0).position.x, Input.GetTouch(0).position.y);
			}
		} else {
			if (Input.GetMouseButton(0)) {
				virtualKeyPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y);
			}
		}

		if (drawArea.Contains(virtualKeyPosition)) {

			if (Input.GetMouseButtonDown(0)) {

				if (recognized) {

					recognized = false;
					strokeId = -1;

					points.Clear();

					foreach (LineRenderer lineRenderer in gestureLinesRenderer) {

						lineRenderer.SetVertexCount(0);
						Destroy(lineRenderer.gameObject);
					}

					gestureLinesRenderer.Clear();
				}

				++strokeId;
				
				Transform tmpGesture = Instantiate(gestureOnScreenPrefab, transform.position, transform.rotation) as Transform;
				currentGestureLineRenderer = tmpGesture.GetComponent<LineRenderer>();
				//Selection.activeGameObject = tmpGesture.gameObject;
				
				gestureLinesRenderer.Add(currentGestureLineRenderer);
				
				vertexCount = 0;
			}
			
			if (Input.GetMouseButton(0)) {
				points.Add(new Point(virtualKeyPosition.x, -virtualKeyPosition.y, strokeId));

				currentGestureLineRenderer.SetVertexCount(++vertexCount);
				currentGestureLineRenderer.SetPosition(vertexCount - 1, Camera.main.ScreenToWorldPoint(new Vector3(virtualKeyPosition.x, virtualKeyPosition.y, 10)));
			}
		}
	}

    public void TryRecognize()
    {
        if (points.Count <= 0)
            return;

        if (recognized)
            ClearLine();

        recognized = true;

        Gesture candidate = new Gesture(points.ToArray());

        Result gestureResult = PointCloudRecognizer.Classify(candidate, trainingSet.ToArray());

        if (gestureResult.Score < .75f)
        {
            ClearLine();
            return;
        }

        if (gestureResult.GestureClass == "cherrybomb" || gestureResult.GestureClass == "sun")
        {
            Vector3 gestureCenter = gestureLinesRenderer[0].bounds.center;
            Vector3 rayDirection = Camera.main.transform.forward;
            RaycastHit hit;

            if (!Physics.Raycast(gestureCenter, rayDirection, out hit, 100f))
            {
                foreach (LineRenderer line in gestureLinesRenderer)
                {
                    GameObject converter = new GameObject("InkConverter");
                    InkToParticles inkConverter = converter.AddComponent<InkToParticles>();
                    inkConverter.particlePrefab = inkParticlePrefab;
                    inkConverter.fadeDuration = 0.7f;
                    inkConverter.particlesPerUnit = 3;

                    inkConverter.ConvertLineToParticles(line);

                    Destroy(line.gameObject, 3f);
                    Destroy(converter, 2f);
                }

 
                Vector3 directionAway = (gestureCenter - Camera.main.transform.position).normalized;
                Vector3 sunPosition = gestureCenter + directionAway * 20f;


                Transform sun = Instantiate(sunPrefab, sunPosition, Quaternion.identity);


                sun.LookAt(Camera.main.transform);

                FindObjectOfType<SkyboxController>().SetDay();

                if (recognized)
                {
                    recognized = false;
                    strokeId = -1;
                    points.Clear();
                    gestureLinesRenderer.Clear();
                }
            }
            else
            {
                Camera.main.GetComponent<CinemachineImpulseSource>().GenerateImpulse();
                Transform b = Instantiate(spherePrefab, gestureLinesRenderer[0].bounds.center, Quaternion.identity);
                b.DOScale(0, .2f).From().SetEase(Ease.OutBack);

                if (recognized)
                {
                    recognized = false;
                    strokeId = -1;

                    points.Clear();

                    foreach (LineRenderer lineRenderer in gestureLinesRenderer)
                    {
                        lineRenderer.SetVertexCount(0);
                        Destroy(lineRenderer.gameObject);
                    }
                    gestureLinesRenderer.Clear();
                }
            }
        }

        if (gestureResult.GestureClass == "moon")
        {
            Vector3 gestureCenter = gestureLinesRenderer[0].bounds.center;
            Vector3 rayDirection = Camera.main.transform.forward;
            RaycastHit hit;

            if (!Physics.Raycast(gestureCenter, rayDirection, out hit, 100f))
            {

                foreach (LineRenderer line in gestureLinesRenderer)
                {
                    GameObject converter = new GameObject("InkConverter");
                    InkToParticles inkConverter = converter.AddComponent<InkToParticles>();
                    inkConverter.particlePrefab = inkParticlePrefab;
                    inkConverter.fadeDuration = 0.7f;
                    inkConverter.particlesPerUnit = 3;
                    inkConverter.particleColor = new Color(0.5f, 0.5f, 1f, 1f);  

                    inkConverter.ConvertLineToParticles(line);

                    Destroy(line.gameObject, 3f);
                    Destroy(converter, 2f);
                }


                Vector3 directionAway = (gestureCenter - Camera.main.transform.position).normalized;
                Vector3 moonPosition = gestureCenter + directionAway * 20f;

                Transform moon = Instantiate(moonPrefab, moonPosition, Quaternion.identity);

                moon.LookAt(Camera.main.transform);

                FindObjectOfType<SkyboxController>().SetNight();

                if (recognized)
                {
                    recognized = false;
                    strokeId = -1;
                    points.Clear();
                    gestureLinesRenderer.Clear();
                }
            }
            else
            {
                ClearLine();
            }
        }

        if (gestureResult.GestureClass == "horizontal line" || gestureResult.GestureClass == "line")
        {
            RaycastHit hit = new RaycastHit();
            if (Physics.SphereCast(gestureLinesRenderer[0].bounds.center, 3, Camera.main.transform.forward, out hit, 15, layerMask))
            {
                if (hit.collider.CompareTag("Cuttable"))
                {
                    hit.collider.GetComponent<TreeScript>().Slash();
                    Camera.main.GetComponent<CinemachineImpulseSource>().GenerateImpulse();
                }
            }

            if (recognized)
            {
                recognized = false;
                strokeId = -1;

                points.Clear();

                foreach (LineRenderer lineRenderer in gestureLinesRenderer)
                {
                    lineRenderer.SetVertexCount(0);
                    Destroy(lineRenderer.gameObject);
                }
                gestureLinesRenderer.Clear();
            }
        }


        if (gestureResult.GestureClass == "plant")
        {
            Vector3 gestureCenter = gestureLinesRenderer[0].bounds.center;
            Vector3 rayDirection = Camera.main.transform.forward;
            RaycastHit hit;


            if (Physics.Raycast(gestureCenter, rayDirection, out hit, 100f))
            {

                foreach (LineRenderer line in gestureLinesRenderer)
                {
                    GameObject converter = new GameObject("InkConverter");
                    InkToParticles inkConverter = converter.AddComponent<InkToParticles>();
                    inkConverter.particlePrefab = inkParticlePrefab;
                    inkConverter.fadeDuration = 0.7f;
                    inkConverter.particlesPerUnit = 3;

                    inkConverter.ConvertLineToParticles(line);

                    Destroy(line.gameObject, 3f);
                    Destroy(converter, 2f);
                }

                // gen plant
                Transform plant = Instantiate(plantPrefab, hit.point, Quaternion.identity);

                //  get AlembicStreamPlayer and play animation
                var alembicPlayer = plant.GetComponent<UnityEngine.Formats.Alembic.Importer.AlembicStreamPlayer>();
                if (alembicPlayer != null)
                {
                    alembicPlayer.CurrentTime = 0f; 
                    StartCoroutine(PlayAndDestroy(plant.gameObject, alembicPlayer));
                }
                else
                {
                    Destroy(plant.gameObject, 3f);
                }


                if (recognized)
                {
                    recognized = false;
                    strokeId = -1;
                    points.Clear();
                    gestureLinesRenderer.Clear();
                }
            }
            else
            {
                ClearLine();
            }
        }




    }

    private IEnumerator PlayAndDestroy(GameObject plantObj, UnityEngine.Formats.Alembic.Importer.AlembicStreamPlayer player)
    {
        float duration = player.Duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            player.CurrentTime = elapsed;
            elapsed += Time.deltaTime;
            yield return null;
        }


        yield return new WaitForSeconds(3f);

        Destroy(plantObj);
    }

    public void ClearLine()
	{
		recognized = false;
		strokeId = -1;

		points.Clear();

		foreach (LineRenderer lineRenderer in gestureLinesRenderer)
		{
			lineRenderer.positionCount = 0;
			Destroy(lineRenderer.gameObject);
		}

		gestureLinesRenderer.Clear();
	}

    void OnGUI()
    {
        if (!gameManager.isDrawing)
            return;

        GUI.Box(drawArea, "Draw Area");

        GUI.Label(new Rect(10, Screen.height - 40, 500, 50), message);

        if (GUI.Button(new Rect(Screen.width - 100, 10, 100, 30), "Recognize"))
        {
            if (points.Count > 0 && trainingSet.Count > 0)
            {
                recognized = true;
                Gesture candidate = new Gesture(points.ToArray());
                Result gestureResult = PointCloudRecognizer.Classify(candidate, trainingSet.ToArray());
                message = gestureResult.GestureClass + " " + gestureResult.Score;

                if (gestureResult.GestureClass == "circle")
                {
                    print("worked");
                }
            }
        }

        GUI.Label(new Rect(Screen.width - 200, 150, 70, 30), "Add as: ");
        newGestureName = GUI.TextField(new Rect(Screen.width - 150, 150, 100, 30), newGestureName);

        if ((GUI.Button(new Rect(Screen.width - 50, 150, 50, 30), "Add") || Input.GetKeyDown(KeyCode.Return))
            && points.Count > 0 && newGestureName != "")
        {
            string fileName = String.Format("{0}/{1}-{2}.xml", Application.persistentDataPath, newGestureName, DateTime.Now.ToFileTime());

#if !UNITY_WEBPLAYER
            GestureIO.WriteGesture(points.ToArray(), newGestureName, fileName);
#endif

            trainingSet.Add(new Gesture(points.ToArray(), newGestureName));

            message = "saved: " + newGestureName + " (" + points.Count + " points)";
            Debug.Log("saved to: " + fileName);

            newGestureName = "";

            ClearLine();
        }
    }
}

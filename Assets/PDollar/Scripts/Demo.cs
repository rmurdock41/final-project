using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using PDollarGestureRecognizer;
using Cinemachine;

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
    [Range(0f, 1f)] public float minimumRecognitionScore = 0.7f;
    public float plantRaycastDistance = 500f;

    private List<Gesture> trainingSet = new List<Gesture>();

	private List<Point> points = new List<Point>();
	private int strokeId = -1;

	private Vector3 virtualKeyPosition = Vector2.zero;
	private Rect drawArea;
	[SerializeField] private bool showGestureDebugUI = false;

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
		if (gameManager == null) {
			Debug.LogError("Demo requires a GameManager in the scene.", this);
			enabled = false;
			return;
		}

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

		// The Game view / standalone window can change size after Start. Keep the
		// input sampling rectangle matched to the actual drawable screen so no
		// part of the visible brush canvas becomes a dead zone.
		if (drawArea.width != Screen.width || drawArea.height != Screen.height)
			drawArea = new Rect(0, 0, Screen.width, Screen.height);

		bool pointerDown = false;
		bool pointerHeld = false;

		if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer) {
			if (Input.touchCount > 0) {
				Touch touch = Input.GetTouch(0);
				virtualKeyPosition = touch.position;
				pointerDown = touch.phase == TouchPhase.Began;
				pointerHeld = touch.phase == TouchPhase.Began ||
				              touch.phase == TouchPhase.Moved ||
				              touch.phase == TouchPhase.Stationary;
			}
		} else {
			pointerDown = Input.GetMouseButtonDown(0);
			pointerHeld = Input.GetMouseButton(0);
			if (pointerHeld) {
				virtualKeyPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y);
			}
		}

		if (drawArea.Contains(virtualKeyPosition)) {

			if (pointerDown) {

				if (recognized) {

					recognized = false;
					strokeId = -1;

					points.Clear();

					foreach (LineRenderer lineRenderer in gestureLinesRenderer) {

						lineRenderer.positionCount = 0;
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
			
			if (pointerHeld && currentGestureLineRenderer != null) {
				points.Add(new Point(virtualKeyPosition.x, -virtualKeyPosition.y, strokeId));

				currentGestureLineRenderer.positionCount = ++vertexCount;
				currentGestureLineRenderer.SetPosition(vertexCount - 1, Camera.main.ScreenToWorldPoint(new Vector3(virtualKeyPosition.x, virtualKeyPosition.y, 10)));
			}
		}
	}

    public void TryRecognize()
    {
        if (points.Count <= 0)
            return;

        if (trainingSet.Count == 0)
        {
            Debug.LogWarning("No gesture templates were loaded.", this);
            ClearLine();
            return;
        }

        if (recognized)
            ClearLine();

        recognized = true;

        Gesture candidate = new Gesture(points.ToArray());

        Result gestureResult = PointCloudRecognizer.Classify(candidate, trainingSet.ToArray());
        string gestureClass = gestureResult.GestureClass.Trim().ToLowerInvariant().Replace('_', ' ');
        message = gestureClass + " " + gestureResult.Score.ToString("0.00");
        Debug.Log("Gesture recognized as " + message, this);

        if (gestureResult.Score < minimumRecognitionScore)
        {
            Debug.LogWarning("Gesture confidence was too low: " + message, this);
            PlayFailedInk();
            return;
        }

        bool gestureClassHandled = false;

        if (gestureClass == "cherrybomb" || gestureClass == "sun")
        {
            gestureClassHandled = true;
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

                SkyboxController skyboxController = FindObjectOfType<SkyboxController>();
                if (skyboxController != null)
                    skyboxController.SetDay();

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
                CinemachineImpulseSource impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();
                if (impulseSource != null)
                    impulseSource.GenerateImpulse();
                Transform b = Instantiate(spherePrefab, gestureLinesRenderer[0].bounds.center, Quaternion.identity);
                b.DOScale(0, .2f).From().SetEase(Ease.OutBack);

                if (recognized)
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
            }
        }

        if (gestureClass == "moon")
        {
            gestureClassHandled = true;
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

                SkyboxController skyboxController = FindObjectOfType<SkyboxController>();
                if (skyboxController != null)
                    skyboxController.SetNight();

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
                PlayFailedInk();
            }
        }

        if (gestureClass == "horizontal line" || gestureClass == "line")
        {
            gestureClassHandled = true;
            bool cutSucceeded = false;
            RaycastHit hit = new RaycastHit();
            if (Physics.SphereCast(gestureLinesRenderer[0].bounds.center, 3, Camera.main.transform.forward, out hit, 15, layerMask))
            {
                if (hit.collider.CompareTag("Cuttable"))
                {
                    cutSucceeded = true;
                    TreeScript tree = hit.collider.GetComponentInParent<TreeScript>();
                    if (tree != null)
                        tree.Slash();

                    CinemachineImpulseSource impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();
                    if (impulseSource != null)
                        impulseSource.GenerateImpulse();

                }
            }

            if (cutSucceeded)
                ClearLine();
            else
                PlayFailedInk();
        }

        if (gestureClass == "plant")
        {
            gestureClassHandled = true;
            if (plantPrefab == null)
            {
                Debug.LogError("The plant prefab is not assigned on Demo.", this);
                ClearLine();
                return;
            }

            Vector3 spawnPosition;
            if (TryGetPlantSpawnPosition(out spawnPosition))
            {
                Instantiate(plantPrefab, spawnPosition + Vector3.down * 0.2f, Quaternion.identity);
                ClearLine();
            }
            else
            {
                Debug.LogWarning("Plant gesture recognized, but no ground was found in front of the camera.", this);
                PlayFailedInk();
            }
        }

        if (!gestureClassHandled && gestureLinesRenderer.Count > 0)
        {
            Debug.LogWarning("No brush action is assigned to gesture: " + gestureClass, this);
            PlayFailedInk();
        }

    }

    private void PlayFailedInk()
    {
        Camera effectCamera = Camera.main;
        foreach (LineRenderer lineRenderer in gestureLinesRenderer)
        {
            if (lineRenderer == null)
                continue;

            FailedInkDissolve.Play(lineRenderer, effectCamera);
            Destroy(lineRenderer.gameObject);
        }

        recognized = false;
        strokeId = -1;
        points.Clear();
        gestureLinesRenderer.Clear();
        currentGestureLineRenderer = null;
    }

    private bool TryGetPlantSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;

        Camera camera = Camera.main;
        if (camera == null || points.Count == 0)
            return false;

        Point bottomPoint = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            // Gesture points store screen Y negated for the recognizer. The
            // visually lowest screen point therefore has the greatest stored Y.
            if (points[i].Y > bottomPoint.Y)
                bottomPoint = points[i];
        }

        Vector3 screenPoint = new Vector3(bottomPoint.X, -bottomPoint.Y, 0f);
        RaycastHit groundHit;
        if (TryRaycastGround(camera.ScreenPointToRay(screenPoint), out groundHit))
        {
            spawnPosition = groundHit.point;
            return true;
        }

        Vector3 groundForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        if (groundForward.sqrMagnitude < 0.001f)
            groundForward = camera.transform.forward;

        Vector3 fallbackOrigin = camera.transform.position + groundForward * 6f + Vector3.up * 10f;
        if (TryRaycastGround(new Ray(fallbackOrigin, Vector3.down), out groundHit))
        {
            spawnPosition = groundHit.point;
            return true;
        }

        return false;
    }

    private bool TryRaycastGround(Ray ray, out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            plantRaycastDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Ground"))
            {
                groundHit = hit;
                return true;
            }
        }

        groundHit = new RaycastHit();
        return false;
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
		// This is the original $P recognizer's gesture-authoring UI. It is useful
		// when creating templates, but should not draw a framed debug area over
		// the actual game canvas.
		if (!gameManager.isDrawing || !showGestureDebugUI)
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

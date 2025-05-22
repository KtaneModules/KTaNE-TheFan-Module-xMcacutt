using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using KModkit;
using UnityEngine.Serialization;
using Random = System.Random;

public class TheFanScript : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;
	public MeshRenderer powerLightMeshRenderer;
	public Material powerLightOnMat;
	public Material powerLightOffMat;

	private static int _moduleIdCounter = 1;
	private int moduleId;
	private bool _isSolved;
	private List<string> _currentSolves = new List<string>();

	private const string Vowels = "AEIOU";
	private const string PossibleCharacters = "0123456789AbcdefGHijKlMnOPqrsTUVwXYZ";
	private static readonly Random Random = new Random();

	private static readonly string[] IgnoredModules = { "The Fan", "Souvenir", "The Heart", "The Swan", "+", "14", "42", "501", "A>N<D", "Bamboozling Time Keeper", "Black Arrows", "Brainf---", "Busy Beaver", "Cube Synchronization", "Don't Touch Anything", "Floor Lights", "Forget Any Color", "Forget Enigma", "Forget Everything", "Forget Infinity", "Forget Maze Not", "Forget It Not", "Forget Me Not", "Forget Me Later", "Forget Perspective", "Forget The Colors", "Forget This", "Forget Them All", "Forget Us Not", "Iconic", "Keypad Directionality", "Kugelblitz", "Multitask", "OmegaDestroyer", "OmegaForget", "Organization", "Password Destroyer", "Purgatory", "RPS Judging", "Security Council", "Shoddy Chess", "Simon Forgets", "Simon's Stages", "Soulscream", "Souvenir", "Tallordered Keys", "The Time Keeper", "The Troll", "The Twin", "The Very Annoying Button", "Timing is Everything", "Turn The Key", "Ultimate Custom Night", "Whiteout", "Übermodule" };
	public GameObject fanBlades;
	private float _fanSpeed;
	private float _fanTargetSpeed;
	private int _direction;
	private bool _isOn;
	private int _solvePercentageRequired;
	private bool _isDeactivated;
	public TextMesh displayText; 

	public KMSelectable directionButton;
	public KMSelectable powerButton;

	public float rotationSpeed = 10f;

	private int _powerButtonCount = 0;
	private const float PowerButtonResetDelay = 0.5f;
	private float _powerButtonPressTime;
	void Awake()
	{
		moduleId = _moduleIdCounter++;
		_direction = 1;

		powerButton.OnInteract += () =>
		{
			powerButton.AddInteractionPunch();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, powerButton.transform);
			if (_isSolved)
				return false;
			_isOn = !_isOn;
			powerLightMeshRenderer.material = _isOn ? powerLightOnMat : powerLightOffMat;
			_powerButtonCount++;
			_powerButtonPressTime = Time.time;
			if (_powerButtonCount < 2) 
				return false;
			_powerButtonCount = 0;
			if (_isDeactivated)
			{
				_isSolved = true;
				_isOn = false;
				powerLightMeshRenderer.material = _isOn ? powerLightOnMat : powerLightOffMat;
				Module.HandlePass();
			}
			else
				Module.HandleStrike();
			return false;
		};
		
		directionButton.OnInteract += () =>
		{
			directionButton.AddInteractionPunch();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, directionButton.transform);
			if (_isOn)
				_direction *= -1;
			return false;
		};
	}
	
	void Start ()
	{
		_fanSpeed = 0;
		displayText.text = GetRandomDisplayText();
		_currentSolves = Bomb.GetSolvedModuleNames();
		_solvePercentageRequired = Random.Next(10, 90);
	}

	public int tickFrequency = 120;
	private float _lastAngle;

	private void FixedUpdate()
	{
		var currentAngle = fanBlades.transform.localRotation.eulerAngles.y;
		var deltaAngle = Mathf.DeltaAngle(_lastAngle, currentAngle); // Handles wraparound
		var ticksPerRevolution = 360f / tickFrequency;

		// Only proceed if the fan is spinning fast enough
		if (Math.Abs(_fanSpeed) > 0.05f && Mathf.Abs(deltaAngle) > 0.01f)
		{
			// Check how many tick boundaries were crossed
			float previousTick = Mathf.Floor(_lastAngle / tickFrequency);
			float currentTick = Mathf.Floor(currentAngle / tickFrequency);

			if (Math.Abs(previousTick - currentTick) > 0.2f)
			{
				Audio.PlaySoundAtTransform("FanTick4", fanBlades.transform);
			}
		}

		_lastAngle = currentAngle;
	}

	void Update()
	{
		_fanTargetSpeed = _isOn ? rotationSpeed * _direction : 0;
		_fanSpeed = Mathf.Lerp(_fanSpeed, _fanTargetSpeed, Time.deltaTime);
		if (Math.Abs(_fanSpeed) < 0.05f)
			_fanSpeed = 0;
		_fanSpeed = Mathf.Clamp(_fanSpeed, rotationSpeed * -1, rotationSpeed);
		fanBlades.transform.Rotate(Vector3.up, _fanSpeed);

		if (_powerButtonCount > 0 && Time.time > _powerButtonPressTime + PowerButtonResetDelay)
			_powerButtonCount = 0;
		
		var unsolvedModules = 
			Bomb.GetSolvableModuleNames().Where(x => !IgnoredModules.Contains(x));
		
		if ((float)Bomb.GetSolvedModuleNames().Count / Bomb.GetModuleNames().Count * 100 >= _solvePercentageRequired
		    || Bomb.GetSolvableModuleNames().All(x => IgnoredModules.Contains(x)))
		{
			_isDeactivated = true;
			displayText.text = "POwer";
			return;
		}

		if (_isDeactivated) return;
		var solvedModules = Bomb.GetSolvedModuleNames();
		if (_currentSolves.Count == solvedModules.Count) 
			return;
		var lastSolved = GetLatestSolve(solvedModules, _currentSolves).ToUpperInvariant();
		var solution = GetAnswer(lastSolved);
		displayText.text = GetRandomDisplayText();
		if (Math.Abs(_fanSpeed - rotationSpeed) < 0.05f && _direction == solution)
			return;
		if (solution == 0 && _fanSpeed < 0.05f)
			return;
		Module.HandleStrike();
	}
	
	private static string GetRandomDisplayText()
	{
		var result = "";
		for (var i = 0; i < 5; i++)
			result += PossibleCharacters[Random.Next(PossibleCharacters.Length)];
		return result;
	}

	private int GetAnswer(string lastSolved)
	{
		var solves = Bomb.GetSolvedModuleNames().Count - 1;
		var serial = Bomb.GetSerialNumber();
		var x = solves;
		foreach (var c in serial)
		{
			if (char.IsDigit(c))
				x += c - '0';
			else if (char.IsLetter(c))
				x -= char.ToUpper(c) - '@';
		}
		x = ((x - 1) % 26 + 26) % 26 + 1;
		var xChar = (char)('@' + x);
	    
		var y = 0;
		foreach (var c in lastSolved)
		{
			if (char.IsDigit(c))
				y += c - '0';
			else if (char.IsLetter(c))
				y += char.ToUpper(c) - '@';
		}
    	
		foreach (var c in displayText.text)
		{
			if (char.IsDigit(c))
				y -= c - '0';
			else if (char.IsLetter(c))
				y -= char.ToUpper(c) - '@';
		}
		y = ((y - 1) % 26 + 26) % 26 + 1;
		var yChar = (char)('@' + y);

		if (Vowels.Contains(xChar) && Vowels.Contains(yChar))
			return 0;
		if (lastSolved.Contains(xChar) && lastSolved.Contains(yChar))
			return 1;
		if (serial.Contains(xChar) && serial.Contains(yChar))
			return -1;
	    
		var z = (x + y) % 3 - 1;
		switch (z)
		{
			case 0:
				return 0;
			case 1:
				return 1;
			case -1:
				return -1;
			default:
				return 0;
		}
	}

	private string GetLatestSolve(ICollection<string> solvedModules, ICollection<string> currentSolves)
	{
		for (var i = 0; i < currentSolves.Count; i++)
			solvedModules.Remove(currentSolves.ElementAt(i));
		for (var i = 0; i < solvedModules.Count; i++)
			_currentSolves.Add(solvedModules.ElementAt(i));
		return solvedModules.ElementAt(0);
	}
}

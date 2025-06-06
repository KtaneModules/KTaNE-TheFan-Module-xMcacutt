using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine.Serialization;
using Random = System.Random;

public class TheFanScript : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;
	public KMBossModule BossModule;
	public MeshRenderer powerLightMeshRenderer;
	public Material powerLightOnMat;
	public Material powerLightOffMat;
	public ParticleSystem MistParticles;

	private static int _moduleIdCounter = 1;
	private int moduleId;
	private bool _isSolved;
	private List<string> _currentSolves = new List<string>();

	private const string Vowels = "AEIOU";
	private const string PossibleCharacters = "0123456789AbcdefGHIjKlmnoPqRsTUVwXyz";
	private static readonly Random Random = new Random();

	private static readonly string[] localIgnored = { "The Fan", "Souvenir", "The Heart", "The Swan", "+", "14", "42", "501", "A>N<D", "Bamboozling Time Keeper", "Black Arrows", "Brainf---", "Busy Beaver", "Cube Synchronization", "Don't Touch Anything", "Floor Lights", "Forget Any Color", "Forget Enigma", "Forget Everything", "Forget Infinity", "Forget Maze Not", "Forget It Not", "Forget Me Not", "Forget Me Later", "Forget Perspective", "Forget The Colors", "Forget This", "Forget Them All", "Forget Us Not", "Iconic", "Keypad Directionality", "Kugelblitz", "Multitask", "OmegaDestroyer", "OmegaForget", "Organization", "Password Destroyer", "Purgatory", "RPS Judging", "Security Council", "Shoddy Chess", "Simon Forgets", "Simon's Stages", "Soulscream", "Souvenir", "Tallordered Keys", "The Time Keeper", "The Troll", "The Twin", "The Very Annoying Button", "Timing is Everything", "Turn The Key", "Ultimate Custom Night", "Whiteout", "Übermodule" };
	private static string[] remoteIgnored;
	private static string[] allIgnored;
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

	public float rotationSpeed = 15f;

	private int _powerButtonCount = 0;
	private const float PowerButtonResetDelay = 0.5f;
	private float _powerButtonPressTime;

	bool PressPower()
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
	}

	bool PressDirection()
	{
		directionButton.AddInteractionPunch();
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, directionButton.transform);
		if (_isOn)
			_direction *= -1;
		return false;
	}
	
	void Awake()
	{
		moduleId = _moduleIdCounter++;
		_direction = 1;

		powerButton.OnInteract += PressPower;
		directionButton.OnInteract += PressDirection;
	}
	
	void Start ()
	{
		remoteIgnored = BossModule.GetIgnoredModules("The Fan");
		allIgnored = remoteIgnored.Union(localIgnored).ToArray();
		_fanSpeed = 0;
		displayText.text = GetRandomDisplayText();
		_currentSolves = Bomb.GetSolvedModuleNames();
		_solvePercentageRequired = Random.Next(30, 50);
		Debug.LogFormat("[The Fan #{0}] Solves required for deactivation: {1}", moduleId, 
			(_solvePercentageRequired / 100) * Bomb.GetSolvableModuleNames().Count(x => !allIgnored.Contains(x)));
		StartCoroutine(Spin());
	}
	
	void Update()
	{
		if (_powerButtonCount > 0 && Time.time > _powerButtonPressTime + PowerButtonResetDelay)
			_powerButtonCount = 0;
		
		if (_isDeactivated) 
			return;

		// Decide if answer is correct when a new solve is detected.
		var solvedModules = Bomb.GetSolvedModuleNames().Where(x => !allIgnored.Contains(x)).ToList();
		if (_currentSolves.Count == solvedModules.Count) 
			return;
		var lastSolved = GetLatestSolve(solvedModules, _currentSolves).ToUpperInvariant();
		var solution = GetAnswer(lastSolved);
		var answerString = "in an unknown state? How did this happen? Better complain to xMcacutt.";
		switch (solution)
		{
			case -1:
				answerString = "spinning counter-clockwise";
				break;
			case 0:
				answerString = "stationary";
				break;
			case 1:
				answerString = "spinning clockwise";
				break;
		}
		
		var inputString = "in an unknown state? How did this happen? Better complain to xMcacutt.";
		switch (_direction * (_isOn ? 1 : 0))
		{
			case -1:
				inputString = "spinning counter-clockwise";
				break;
			case 0:
				inputString = "stationary";
				break;
			case 1:
				inputString = "spinning clockwise";
				break;
		}
		
		if ((Math.Abs(_fanSpeed - rotationSpeed) > 0.05f && _direction == solution) || (solution == 0 && _fanSpeed < 0.05f))
		{
			// Input was correct!
			Debug.LogFormat("[The Fan #{0}] The Fan was in the correct state! ({1})", moduleId, inputString);
			CheckDeactivated();
			return;
		}

		Debug.LogFormat("[The Fan #{0}] Strike! The fan should have been {1} but it was {2}.", moduleId, answerString, inputString);
		Module.HandleStrike();
		CheckDeactivated();
	}

	private void CheckDeactivated()
	{
		var numSolvable = Bomb.GetSolvableModuleNames().Count(x => !allIgnored.Contains(x));
		var numSolved = Bomb.GetSolvedModuleNames().Count(x => !allIgnored.Contains(x));
		if (!((float)Bomb.GetSolvedModuleNames().Count / Bomb.GetModuleNames().Count * 100 >= _solvePercentageRequired)
		    && numSolved < numSolvable)
		{
			displayText.text = GetRandomDisplayText();
			return;
		}
		_isDeactivated = true;
		displayText.text = "PoweR";
		Debug.LogFormat("[The Fan #{0}] Module deactivated. Double tap power button to solve.", moduleId);
	}
	
	private float _lastSoundTime = -1f; 
	IEnumerator Spin()
	{
		while (true)
		{
			if (Math.Abs(_fanSpeed) < 2)
			{
				MistParticles.gameObject.SetActive(false);
				yield return null;
			}
			
			_fanTargetSpeed = rotationSpeed * _direction * (_isOn ? 1 : 0);
			
			MistParticles.gameObject.SetActive(_fanTargetSpeed != 0);
			_fanSpeed = Mathf.Lerp(_fanSpeed, _fanTargetSpeed, Time.deltaTime);
			fanBlades.transform.Rotate(Vector3.up, _fanSpeed);
			
			var main = MistParticles.main;
			main.startSpeed = Mathf.Clamp(Math.Abs(_fanSpeed), 0.2f, 10f);
			main.startLifetime = Mathf.Clamp(1.5f / Mathf.Log(1 + Math.Abs(_fanSpeed)), 0.5f, 1.5f);
			
			if (Math.Abs(_fanSpeed) > 0.1f) 
			{
				var absFanSpeed = Mathf.Abs(_fanSpeed);
				var tickInterval = 3f / Mathf.Clamp(absFanSpeed, 0.01f, 30f);
				if (Time.time - _lastSoundTime >= tickInterval)
				{
					Audio.PlaySoundAtTransform("FanTick" + Random.Next(1, 3), fanBlades.transform);
					_lastSoundTime = Time.time;
				}
			}
			else
				_fanSpeed = 0;

			yield return null;
		}
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
		Debug.LogFormat("[The Fan #{0}] The value of x is {1} = {2} for module: {3}", moduleId, x, xChar, lastSolved);
	    
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
		Debug.LogFormat("[The Fan #{0}] The value of y is {1} = {2} for module: {3}", moduleId, y, yChar, lastSolved);

		if (Vowels.ToUpper().Contains(xChar) && Vowels.ToUpper().Contains(yChar))
		{
			Debug.LogFormat("[The Fan #{0}] should be stationary because x and y are vowels for module: {1}", moduleId, lastSolved);
			return 0;
		}

		if (lastSolved.ToUpper().Contains(xChar) && lastSolved.ToUpper().Contains(yChar))
		{
			Debug.LogFormat("[The Fan #{0}] should be spinning clockwise because x and y are in the name of the solved module: {1}", moduleId, lastSolved);
			return 1;
		}

		if (serial.ToUpper().Contains(xChar) && serial.ToUpper().Contains(yChar))
		{
			Debug.LogFormat("[The Fan #{0}] should be spinning counter-clockwise because x and y are in the serial number for module: {1}", moduleId, lastSolved);
			return -1;
		}
	    
		var z = (x + y) % 3 - 1;
		switch (z)
		{
			case 0:
				Debug.LogFormat("[The Fan #{0}] should be stationary because (x + y) % 3 - 1 is 0 for module: {1}", moduleId, lastSolved);
				return 0;
			case 1:
				Debug.LogFormat("[The Fan #{0}] should be spinning clockwise because (x + y) % 3 - 1 is 1 for module: {1}", moduleId, lastSolved);
				return 1;
			case -1:
				Debug.LogFormat("[The Fan #{0}] should be spinning counter-clockwise because (x + y) % 3 - 1 is -1 for module: {1}", moduleId, lastSolved);
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
	
#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} pow (toggle power), !{0} dir (toggle direction), !{0} dPow (double tap power)";
#pragma warning restore 414
	
	IEnumerator ProcessTwitchCommand(string command)
	{
		command = command.ToLowerInvariant();
		if (Regex.IsMatch(command, @"^\s*(?:pow)\s*$", RegexOptions.IgnoreCase))
		{
			PressPower();
			yield return true;
		}
		else if (Regex.IsMatch(command, @"^\s*(?:dir)\s*$", RegexOptions.IgnoreCase))
		{
			PressDirection();
			yield return true;
		}
		else if (Regex.IsMatch(command, @"^\s*(?:dPow)\s*$", RegexOptions.IgnoreCase))
		{
			PressPower();
			PressPower();
			yield return true;
		}
		yield return null;
	}
}

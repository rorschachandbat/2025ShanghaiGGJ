using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
	/// <summary>
	/// An event triggered every time health values change, for other classes to listen to
	/// </summary>
	public struct HealthChangeEvent
	{
		public Health AffectedHealth;
		public float NewHealth;
		
		public HealthChangeEvent(Health affectedHealth, float newHealth)
		{
			AffectedHealth = affectedHealth;
			NewHealth = newHealth;
		}

		static HealthChangeEvent e;
		public static void Trigger(Health affectedHealth, float newHealth)
		{
			e.AffectedHealth = affectedHealth;
			e.NewHealth = newHealth;
			MMEventManager.TriggerEvent(e);
		}
	}
	
	public struct HealthDeathEvent
	{
		public Health AffectedHealth;
		
		public HealthDeathEvent(Health affectedHealth)
		{
			AffectedHealth = affectedHealth;
		}

		static HealthDeathEvent e;
		public static void Trigger(Health affectedHealth)
		{
			e.AffectedHealth = affectedHealth;
			MMEventManager.TriggerEvent(e);
		}
	}
	
	/// <summary>
	/// This class manages the health of an object, pilots its potential health bar, handles what happens when it takes damage,
	/// and what happens when it dies.
	/// </summary>
	[AddComponentMenu("Corgi Engine/Character/Core/Health")]
	public class Health : MMMonoBehaviour, MMEventListener<HealthDeathEvent>
	{
		[MMInspectorGroup("Status", true, 1)]
		
		/// the current health of the character
		[MMReadOnly] [Tooltip("the current health of the character")]
		public float CurrentHealth;
		
		/// If this is true, this object can't take damage at the moment
		[MMReadOnly] [Tooltip("If this is true, this object can't take damage at the moment")]
		public bool TemporarilyInvulnerable = false;

		/// If this is true, this object is in post damage invulnerability state
		[MMReadOnly] [Tooltip("If this is true, this object is in post damage invulnerability state")]
		public bool PostDamageInvulnerable = false;
		
		[MMInformation(
			"Add this component to an object and it'll have health, will be able to get damaged and potentially die.",
			MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
		
		[MMInspectorGroup("Health", true, 2)]
		
		/// the initial amount of health of the object
		[Tooltip("the initial amount of health of the object")]
		public float InitialHealth = 10;

		/// the maximum amount of health of the object
		[Tooltip("the maximum amount of health of the object")]
		public float MaximumHealth = 10;

		/// spriteObject
		[Tooltip("spriteObject")]
		public GameObject spriteObject; // 需要缩放的Sprite对象

		/// if this is true, this object can't take damage
		[Tooltip("if this is true, this object can't take damage")]
		public bool Invulnerable = false;

		[MMInspectorGroup("Damage", true, 3)]
		
		[MMInformation(
			"Here you can specify an effect and a sound FX to instantiate when the object gets damaged, and also how long the object should flicker when hit (only works for sprites).",
			MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
        
		/// whether or not this Health object can be damaged, you can play with this on top of Invulnerable, which will be turned on/off temporarily for temporary invulnerability. ImmuneToDamage is more of a permanent solution. 
		[Tooltip("whether or not this Health object can be damaged, you can play with this on top of Invulnerable, which will be turned on/off temporarily for temporary invulnerability. ImmuneToDamage is more of a permanent solution.")]
		public bool ImmuneToDamage = false;
        
		/// the MMFeedbacks to play when the character gets hit
		[Tooltip("the MMFeedbacks to play when the character gets hit")]
		public MMFeedbacks DamageFeedbacks;
        
		/// if this is true, the damage value will be passed to the MMFeedbacks as its Intensity parameter, letting you trigger more intense feedbacks as damage increases
		[Tooltip("if this is true, the damage value will be passed to the MMFeedbacks as its Intensity parameter, letting you trigger more intense feedbacks as damage increases")]
		public bool FeedbackIsProportionalToDamage = false;

		/// should the sprite (if there's one) flicker when getting damage ?
		[Tooltip("should the sprite (if there's one) flicker when getting damage ?")]
		public bool FlickerSpriteOnHit = true;

		/// the color the sprite should flicker to
		[Tooltip("the color the sprite should flicker to")] [MMCondition("FlickerSpriteOnHit", true)]
		public Color FlickerColor = new Color32(255, 20, 20, 255);

		[MMInspectorGroup("Knockback", true, 6)]
		
		/// whether or not this object can get knockback
		[Tooltip("whether or not this object can get knockback")]
		public bool ImmuneToKnockback = false;

		/// whether or not this object is immune to damage knockback if the damage received is zero
		[Tooltip("whether or not this object is immune to damage knockback if the damage received is zero")]
		public bool ImmuneToKnockbackIfZeroDamage = false;

		[MMInspectorGroup("Death", true, 7)]
		
		[MMInformation(
			"Here you can set an effect to instantiate when the object dies, a force to apply to it (corgi controller required), how many points to add to the game score, and where the character should respawn (for non-player characters only).",
			MoreMountains.Tools.MMInformationAttribute.InformationType.Info, false)]
		/// the MMFeedbacks to play when the character dies
		[Tooltip("the MMFeedbacks to play when the character dies")]
		public MMFeedbacks DeathFeedbacks;

		/// if this is not true, the object will remain there after its death
		[Tooltip("if this is not true, the object will remain there after its death")]
		public bool DestroyOnDeath = true;

		/// the time (in seconds) before the character is destroyed or disabled
		[Tooltip("the time (in seconds) before the character is destroyed or disabled")]
		public float DelayBeforeDestruction = 0f;

		/// if this is true, collisions will be turned off when the character dies
		[Tooltip("if this is true, collisions will be turned off when the character dies")]
		public bool CollisionsOffOnDeath = true;

		/// if this is true, gravity will be turned off on death
		[Tooltip("if this is true, gravity will be turned off on death")]
		public bool GravityOffOnDeath = false;

		/// the points the player gets when the object's health reaches zero
		[Tooltip("the points the player gets when the object's health reaches zero")]
		public int PointsWhenDestroyed;

		/// the health the player gets when the object's health reaches zero
		[Tooltip("the health the player gets when the object's health reaches zero")]
		public int HealthWhenDestroyed;

		/// if this is set to false, the character will respawn at the location of its death, otherwise it'll be moved to its initial position (when the scene started)
		[Tooltip(
			"if this is set to false, the character will respawn at the location of its death, otherwise it'll be moved to its initial position (when the scene started)")]
		public bool RespawnAtInitialLocation = false;

		[MMInspectorGroup("Death Forces", true, 10)]
		
		/// whether or not to apply a force on death
		[Tooltip("whether or not to apply a force on death")]
		public bool ApplyDeathForce = true;

		/// the force applied when the character dies
		[Tooltip("the force applied when the character dies")]
		public Vector2 DeathForce = new Vector2(0, 10);

		/// whether or not the controller's forces should be set to 0 on death
		[Tooltip("whether or not the controller's forces should be set to 0 on death")]
		public bool ResetForcesOnDeath = false;
        
		/// if this is true, color will be reset on revive
		[Tooltip("if this is true, color will be reset on revive")]
		public bool ResetColorOnRevive = true;
		/// the name of the property on your renderer's shader that defines its color 
		[Tooltip("the name of the property on your renderer's shader that defines its color")]
		[MMCondition("ResetColorOnRevive", true)]
		public string ColorMaterialPropertyName = "_Color";
		/// if this is true, this component will use material property blocks instead of working on an instance of the material.
		[Tooltip("if this is true, this component will use material property blocks instead of working on an instance of the material.")] 
		public bool UseMaterialPropertyBlocks = false;

		[MMInspectorGroup("Shared Health and Damage Resistance", true, 11)]

		/// the Character this Health should impact, if left empty, will pick one on the same game object
		[Tooltip("the Character this Health should impact, if left empty, will pick one on the same game object")]
		public Character AssociatedCharacter;
		
		/// another Health component (usually on another character) towards which all health will be redirected
		[Tooltip("another Health component (usually on another character) towards which all health will be redirected")]
		public Health MasterHealth;

		/// if this is true, when using a MasterHealth, this Health won't take damage, and all damage will be redirected. If this is false, this Health will be able to die when its own Health is consumed
		[Tooltip("if this is true, when using a MasterHealth, this Health won't take damage, and all damage will be redirected. If this is false, this Health will be able to die when its own Health is consumed")]
		public bool OnlyDamageMaster = true;

		/// if this is true, when using a MasterHealth, if the MasterHealth dies, this Health will also die
		[Tooltip("if this is true, when using a MasterHealth, if the MasterHealth dies, this Health will also die")]
		public bool KillOnMasterHealthDeath = false;
		
		/// a DamageResistanceProcessor this Health will use to process damage when it's received
		[Tooltip("a DamageResistanceProcessor this Health will use to process damage when it's received")]
		public DamageResistanceProcessor TargetDamageResistanceProcessor;

		public float LastDamage { get; set; }
		public Vector3 LastDamageDirection { get; set; }
		public bool Initialized => _initialized;
		public CorgiController AssociatedController => _controller;

		// respawn
		public delegate void OnHitDelegate();
		public delegate void OnHitZeroDelegate();
		public delegate void OnReviveDelegate();
		public delegate void OnDeathDelegate();
		
		public OnDeathDelegate OnDeath;
		public OnHitDelegate OnHit;
		public OnHitZeroDelegate OnHitZero;
		public OnReviveDelegate OnRevive;

		protected CharacterHorizontalMovement _characterHorizontalMovement;
		protected Vector3 _initialPosition;
		protected Color _initialColor;
		protected Renderer _renderer;
		protected Character _character;
		protected CorgiController _controller;
		protected MMHealthBar _healthBar;
		protected Collider2D _collider2D;
		protected bool _initialized = false;
		protected AutoRespawn _autoRespawn;
		protected Animator _animator;
		protected CharacterPersistence _characterPersistence = null;
		protected MaterialPropertyBlock _propertyBlock;
		protected bool _hasColorProperty = false;
		protected GameObject _thisObject;
		protected class InterruptiblesDamageOverTimeCoroutine
		{
			public Coroutine DamageOverTimeCoroutine;
			public DamageType DamageOverTimeType;
		}

		protected List<InterruptiblesDamageOverTimeCoroutine> _interruptiblesDamageOverTimeCoroutines;
		protected List<InterruptiblesDamageOverTimeCoroutine> _damageOverTimeCoroutines;

		/// <summary>
		/// On Awake, we initialize our health
		/// </summary>
		protected virtual void Start()
		{
			Initialization();
			InitializeSpriteColor();
			InitializeCurrentHealth();
		}

		/// <summary>
		/// Grabs useful components, enables damage and gets the inital color
		/// </summary>
		protected virtual void Initialization()
		{
			_character = (AssociatedCharacter == null) ? this.gameObject.GetComponent<Character>() : AssociatedCharacter;

			if (_character != null)
			{
				_thisObject = _character.gameObject;
				_characterPersistence = _character.FindAbility<CharacterPersistence>();	
			}
			else
			{
				_thisObject = this.gameObject;
			}

			if (this.gameObject.MMGetComponentNoAlloc<SpriteRenderer>() != null)
			{
				_renderer = this.gameObject.GetComponent<SpriteRenderer>();
			}

			if (_character != null)
			{
				if (_character.CharacterModel != null)
				{
					if (_character.CharacterModel.GetComponentInChildren<Renderer>() != null)
					{
						_renderer = _character.CharacterModel.GetComponentInChildren<Renderer>();
					}
				}
				
				if (_character.CharacterAnimator != null)
				{
					_animator = _character.CharacterAnimator;
				}
				else
				{
					_animator = this.gameObject.GetComponent<Animator>();
				}

				_characterHorizontalMovement = _character.FindAbility<CharacterHorizontalMovement>();
			}
			else
			{
				_animator = this.gameObject.GetComponent<Animator>();
			}

			if (_animator != null)
			{
				_animator.logWarnings = false;
			}

			_autoRespawn = _thisObject.GetComponent<AutoRespawn>();
			_controller = _thisObject.GetComponent<CorgiController>();
			_healthBar = _thisObject.GetComponent<MMHealthBar>();
			_collider2D = _thisObject.GetComponent<Collider2D>();
			
			_interruptiblesDamageOverTimeCoroutines = new List<InterruptiblesDamageOverTimeCoroutine>();
			_damageOverTimeCoroutines = new List<InterruptiblesDamageOverTimeCoroutine>();

			_propertyBlock = new MaterialPropertyBlock();
            
			StoreInitialPosition();    
			_initialized = true;
			DamageEnabled();
			DisablePostDamageInvulnerability();
			UpdateHealthBar(false);
			if (_healthBar != null)
			{
				_healthBar.SetInitialActiveState();
			}
		}
		
		/// <summary>
		/// Initializes health to either initial or current values
		/// </summary>
		public virtual void InitializeCurrentHealth()
		{
			if ((MasterHealth == null) || (!OnlyDamageMaster))
			{
				SetHealth(InitialHealth, _thisObject);	
			}
			else
			{
				if (MasterHealth.Initialized)
				{
					SetHealth(MasterHealth.CurrentHealth, _thisObject);
				}
				else
				{
					SetHealth(MasterHealth.InitialHealth, _thisObject);
				}
			}
		}

		public virtual void StoreInitialPosition()
		{
			_initialPosition = transform.position;
		}

		/// <summary>
		/// Stores the inital color of the Character's sprite.
		/// </summary>
		protected virtual void InitializeSpriteColor()
		{
			if (!FlickerSpriteOnHit)
			{
				return;
			}

			if (_renderer != null)
			{
				if (UseMaterialPropertyBlocks && _renderer.HasPropertyBlock())
				{
					if (_renderer.sharedMaterial.HasProperty(ColorMaterialPropertyName))
					{
						_renderer.GetPropertyBlock(_propertyBlock);
						_initialColor = _propertyBlock.GetColor(ColorMaterialPropertyName);
						_renderer.SetPropertyBlock(_propertyBlock);
					}
				}
				else
				{
					if (_renderer.material.HasProperty(ColorMaterialPropertyName))
					{
						_hasColorProperty = true;
						_initialColor = _renderer.material.GetColor(ColorMaterialPropertyName);
					} 
				}
			}
		}

		/// <summary>
		/// Restores the original sprite color
		/// </summary>
		protected virtual void ResetSpriteColor()
		{
			if (_renderer != null)
			{
				if (UseMaterialPropertyBlocks && _renderer.HasPropertyBlock())
				{
					_renderer.GetPropertyBlock(_propertyBlock);
					_propertyBlock.SetColor(ColorMaterialPropertyName, _initialColor);
					_renderer.SetPropertyBlock(_propertyBlock);    
				}
				else
				{
					_renderer.material.SetColor(ColorMaterialPropertyName, _initialColor);
				}
			}
		}
		
		/// <summary>
		/// Returns true if this Health component can be damaged this frame, and false otherwise
		/// </summary>
		/// <returns></returns>
		public virtual bool CanTakeDamageThisFrame()
		{
			// if the object is invulnerable, we do nothing and exit
			if (Invulnerable || ImmuneToDamage)
			{
				return false;
			}

			if (!this.enabled)
			{
				return false;
			}
			
			// if we're already below zero, we do nothing and exit
			if ((CurrentHealth <= 0) && (InitialHealth != 0))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Called when the object takes damage
		/// </summary>
		/// <param name="damage">The amount of health points that will get lost.</param>
		/// <param name="instigator">The object that caused the damage.</param>
		/// <param name="flickerDuration">The time (in seconds) the object should flicker after taking the damage.</param>
		/// <param name="invincibilityDuration">The duration of the short invincibility following the hit.</param>
		public virtual void Damage(float damage, GameObject instigator, float flickerDuration,
			float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null)
		{
			if (!gameObject.activeInHierarchy)
			{
				return;
			}

			// if the object is invulnerable, we do nothing and exit
			if (TemporarilyInvulnerable || Invulnerable || ImmuneToDamage || PostDamageInvulnerable)
			{
				OnHitZero?.Invoke();
				return;
			}

			if (!CanTakeDamageThisFrame())
			{
				return;
			}

			damage = ComputeDamageOutput(damage, typedDamages, true);
			
			// we process any condition state change
			ComputeCharacterConditionStateChanges(typedDamages);
			ComputeCharacterMovementMultipliers(typedDamages);
			
			if (damage <= 0)
			{
				OnHitZero?.Invoke();
				return;
			}
			
			// we decrease the character's health by the damage
			float previousHealth = CurrentHealth;
			if (MasterHealth != null)
			{
				previousHealth = MasterHealth.CurrentHealth;
				MasterHealth.Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);

				if (!OnlyDamageMaster)
				{
					previousHealth = CurrentHealth;
					SetHealth(CurrentHealth - damage, instigator);	
				}
			}
			else
			{
				SetHealth(CurrentHealth - damage, instigator);	
			}

			LastDamage = damage;
			LastDamageDirection = damageDirection;
			OnHit?.Invoke();

			if (CurrentHealth < 0)
			{
				CurrentHealth = 0;
			}

			// we prevent the character from colliding with Projectiles, Player and Enemies
			if ((invincibilityDuration > 0) && gameObject.activeInHierarchy)
			{
				EnablePostDamageInvulnerability();
				StartCoroutine(DisablePostDamageInvulnerability(invincibilityDuration));
			}

			// we trigger a damage taken event
			MMDamageTakenEvent.Trigger(this, instigator, CurrentHealth, damage, previousHealth);

			if (_animator != null)
			{
				_animator.SetTrigger("Damage");
			}

			// we play the damage feedback
			if (FeedbackIsProportionalToDamage)
			{
				DamageFeedbacks?.PlayFeedbacks(this.transform.position, damage);    
			}
			else
			{
				DamageFeedbacks?.PlayFeedbacks(this.transform.position);
			}

			if (FlickerSpriteOnHit)
			{
				// We make the character's sprite flicker
				if (_renderer != null)
				{
					StartCoroutine(MMImage.Flicker(_renderer, _initialColor, FlickerColor, 0.05f, flickerDuration));
				}
			}

			// we update the health bar
			UpdateHealthBar(true);
			
			// if health has reached zero we set its health to zero (useful for the healthbar)
			if (MasterHealth != null)
			{
				if (MasterHealth.CurrentHealth <= 0)
				{
					MasterHealth.CurrentHealth = 0;
					Kill();
				}
				if (!OnlyDamageMaster)
				{
					if (CurrentHealth <= 0)
					{
						CurrentHealth = 0;
						Kill();
					}
				}
			}
			else
			{
				if (CurrentHealth <= 0)
				{
					CurrentHealth = 0;
					Kill();
				}
			}
		}

		/// <summary>
		/// Kills the character, instantiates death effects, handles points, etc
		/// </summary>
		public virtual void Kill()
		{
			if (ImmuneToDamage)
			{
				return;
			}
			
			if (_character != null)
			{
				// we set its dead state to true
				_character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
				_character.Reset();

				if (_character.CharacterType == Character.CharacterTypes.Player)
				{
					CorgiEngineEvent.Trigger(CorgiEngineEventTypes.PlayerDeath, _character);
				}
			}
			SetHealth(0f, _thisObject);
            
			// we prevent further damage
			DamageDisabled();

			StopAllDamageOverTime();

			// instantiates the destroy effect
			DeathFeedbacks?.PlayFeedbacks();

			// Adds points if needed.
			if (PointsWhenDestroyed != 0)
			{
				// we send a new points event for the GameManager to catch (and other classes that may listen to it too)
				CorgiEnginePointsEvent.Trigger(PointsMethods.Add, PointsWhenDestroyed);
			}

			// Adds health if needed.
			if (HealthWhenDestroyed != 0)
			{

				Health characterHealth = LevelManager.Instance.Players[0].GetComponent<Health>();
				// else, we give health to the player
				characterHealth.GetHealth(HealthWhenDestroyed, gameObject);
			}

			if (_animator != null)
			{
				_animator.SetTrigger("Death");
			}

			if (OnDeath != null)
			{
				OnDeath();
			}
			
			MMLifeCycleEvent.Trigger(this, MMLifeCycleEventTypes.Death);
			
			HealthDeathEvent.Trigger(this);

			// if we have a controller, removes collisions, restores parameters for a potential respawn, and applies a death force
			if (_controller != null)
			{
				// we make it ignore the collisions from now on
				if (CollisionsOffOnDeath)
				{
					_controller.CollisionsOff();
					if (_collider2D != null)
					{
						_collider2D.enabled = false;
					}
				}

				// we reset our parameters
				_controller.ResetParameters();

				if (GravityOffOnDeath)
				{
					_controller.GravityActive(false);
				}

				// we reset our controller's forces on death if needed
				if (ResetForcesOnDeath)
				{
					_controller.SetForce(Vector2.zero);
				}

				// we apply our death force
				if (ApplyDeathForce)
				{
					_controller.GravityActive(true);
					_controller.SetForce(DeathForce);
				}
			}


			// if we have a character, we want to change its state
			if (_character != null)
			{
				// we set its dead state to true
				_character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
				_character.Reset();

				// if this is a player, we quit here
				if (_character.CharacterType == Character.CharacterTypes.Player)
				{
					return;
				}
			}

			if (DelayBeforeDestruction > 0f)
			{
				Invoke("DestroyObject", DelayBeforeDestruction);
			}
			else
			{
				// finally we destroy the object
				DestroyObject();
			}
		}

		/// <summary>
		/// Revive this object.
		/// </summary>
		public virtual void Revive()
		{
			if (!_initialized)
			{
				return;
			}

			if (_characterPersistence != null)
			{
				if (_characterPersistence.Initialized)
				{
					return;
				}
			}

			if (_collider2D != null)
			{
				_collider2D.enabled = true;
			}

			if (_controller != null)
			{
				_controller.CollisionsOn();
				_controller.GravityActive(true);
				_controller.SetForce(Vector2.zero);
				_controller.ResetParameters();
			}

			if (_character != null)
			{
				_character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Normal);
			}

			if (RespawnAtInitialLocation)
			{
				transform.position = _initialPosition;
			}

			Initialization();
			InitializeCurrentHealth();
			if (FlickerSpriteOnHit && ResetColorOnRevive)
			{
				ResetSpriteColor();
			}

			UpdateHealthBar(false);
			if (_healthBar != null)
			{
				_healthBar.SetInitialActiveState();
			}
			if (OnRevive != null)
			{
				OnRevive.Invoke();
			}
			MMLifeCycleEvent.Trigger(this, MMLifeCycleEventTypes.Revive);
		}

		/// <summary>
		/// Destroys the object, or tries to, depending on the character's settings
		/// </summary>
		protected virtual void DestroyObject()
		{
			if (!DestroyOnDeath)
			{
				return;
			}

			if (_autoRespawn == null)
			{
				// object is turned inactive to be able to reinstate it at respawn
				gameObject.SetActive(false);
			}
			else
			{
				_autoRespawn.Kill();
			}
		}
		
		/// <summary>
		/// Interrupts all damage over time, regardless of type
		/// </summary>
		public virtual void InterruptAllDamageOverTime()
		{
			foreach (InterruptiblesDamageOverTimeCoroutine coroutine in _interruptiblesDamageOverTimeCoroutines)
			{
				StopCoroutine(coroutine.DamageOverTimeCoroutine);
			}
			_interruptiblesDamageOverTimeCoroutines.Clear();
		}

		/// <summary>
		/// Interrupts all damage over time, even the non interruptible ones (usually on death)
		/// </summary>
		public virtual void StopAllDamageOverTime()
		{
			foreach (InterruptiblesDamageOverTimeCoroutine coroutine in _damageOverTimeCoroutines)
			{
				StopCoroutine(coroutine.DamageOverTimeCoroutine);
			}
			_damageOverTimeCoroutines.Clear();
		}

		/// <summary>
		/// Interrupts all damage over time of the specified type
		/// </summary>
		/// <param name="damageType"></param>
		public virtual void InterruptAllDamageOverTimeOfType(DamageType damageType)
		{
			foreach (InterruptiblesDamageOverTimeCoroutine coroutine in _interruptiblesDamageOverTimeCoroutines)
			{
				if (coroutine.DamageOverTimeType == damageType)
				{
					StopCoroutine(coroutine.DamageOverTimeCoroutine);	
				}
			}
			TargetDamageResistanceProcessor?.InterruptDamageOverTime(damageType);
		}

		/// <summary>
		/// Applies damage over time, for the specified amount of repeats (which includes the first application of damage, makes it easier to do quick maths in the inspector, and at the specified interval).
		/// Optionally you can decide that your damage is interruptible, in which case, calling InterruptAllDamageOverTime() will stop these from being applied, useful to cure poison for example.
		/// </summary>
		/// <param name="damage"></param>
		/// <param name="instigator"></param>
		/// <param name="flickerDuration"></param>
		/// <param name="invincibilityDuration"></param>
		/// <param name="damageDirection"></param>
		/// <param name="typedDamages"></param>
		/// <param name="amountOfRepeats"></param>
		/// <param name="durationBetweenRepeats"></param>
		/// <param name="interruptible"></param>
		public virtual void DamageOverTime(float damage, GameObject instigator, float flickerDuration,
			float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null,
			int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true, DamageType damageType = null)
		{
			if (ComputeDamageOutput(damage, typedDamages, false) == 0)
			{
				return;
			}

			InterruptiblesDamageOverTimeCoroutine damageOverTime = new InterruptiblesDamageOverTimeCoroutine();
			damageOverTime.DamageOverTimeType = damageType;
			damageOverTime.DamageOverTimeCoroutine = StartCoroutine(DamageOverTimeCo(damage, instigator, flickerDuration,
				invincibilityDuration, damageDirection, typedDamages, amountOfRepeats, durationBetweenRepeats,
				interruptible));
			
			_damageOverTimeCoroutines.Add(damageOverTime);

			if (interruptible)
			{
				_interruptiblesDamageOverTimeCoroutines.Add(damageOverTime);
			}
		}

		/// <summary>
		/// A coroutine used to apply damage over time
		/// </summary>
		/// <param name="damage"></param>
		/// <param name="instigator"></param>
		/// <param name="flickerDuration"></param>
		/// <param name="invincibilityDuration"></param>
		/// <param name="damageDirection"></param>
		/// <param name="typedDamages"></param>
		/// <param name="amountOfRepeats"></param>
		/// <param name="durationBetweenRepeats"></param>
		/// <param name="interruptible"></param>
		/// <param name="damageType"></param>
		/// <returns></returns>
		protected virtual IEnumerator DamageOverTimeCo(float damage, GameObject instigator, float flickerDuration,
			float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null,
			int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true, DamageType damageType = null)
		{
			for (int i = 0; i < amountOfRepeats; i++)
			{
				Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
				yield return MMCoroutine.WaitFor(durationBetweenRepeats);
			}
		}

		/// <summary>
		/// Returns the damage this health should take after processing potential resistances
		/// </summary>
		/// <param name="damage"></param>
		/// <returns></returns>
		public virtual float ComputeDamageOutput(float damage, List<TypedDamage> typedDamages = null, bool damageApplied = false)
		{
			if (TemporarilyInvulnerable || Invulnerable || ImmuneToDamage || PostDamageInvulnerable)
			{
				return 0;
			}
			
			float totalDamage = 0f;
			// we process our damage through our potential resistances
			if (TargetDamageResistanceProcessor != null)
			{
				if (TargetDamageResistanceProcessor.isActiveAndEnabled)
				{
					totalDamage = TargetDamageResistanceProcessor.ProcessDamage(damage, typedDamages, damageApplied);	
				}
			}
			else
			{
				totalDamage = damage;
				if (typedDamages != null)
				{
					foreach (TypedDamage typedDamage in typedDamages)
					{
						totalDamage += typedDamage.DamageCaused;
					}
				}
			}
			return totalDamage;
		}

		/// <summary>
		/// Determines a new knockback force by processing it through resistances
		/// </summary>
		/// <param name="knockbackForce"></param>
		/// <param name="typedDamages"></param>
		/// <returns></returns>
		public virtual Vector2 ComputeKnockbackForce(Vector2 knockbackForce, List<TypedDamage> typedDamages = null)
		{
			return (TargetDamageResistanceProcessor == null) ? knockbackForce : TargetDamageResistanceProcessor.ProcessKnockbackForce(knockbackForce, typedDamages);;
			
		}

		/// <summary>
		/// Returns true if this Health can get knockbacked, false otherwise
		/// </summary>
		/// <param name="typedDamages"></param>
		/// <returns></returns>
		public virtual bool CanGetKnockback(List<TypedDamage> typedDamages) 
		{
			if (ImmuneToKnockback)
			{
				return false;
			}
			if (TargetDamageResistanceProcessor != null)
			{
				if (TargetDamageResistanceProcessor.isActiveAndEnabled)
				{
					bool checkResistance = TargetDamageResistanceProcessor.CheckPreventKnockback(typedDamages);
					if (checkResistance)
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Goes through resistances and applies condition state changes if needed
		/// </summary>
		/// <param name="typedDamages"></param>
		protected virtual void ComputeCharacterConditionStateChanges(List<TypedDamage> typedDamages)
		{
			if ((typedDamages == null) || (_character == null))
			{
				return;
			}

			foreach (TypedDamage typedDamage in typedDamages)
			{
				if (typedDamage.ForceCharacterCondition)
				{
					if (TargetDamageResistanceProcessor != null)
					{
						if (TargetDamageResistanceProcessor.isActiveAndEnabled)
						{
							bool checkResistance =
								TargetDamageResistanceProcessor.CheckPreventCharacterConditionChange(typedDamage.AssociatedDamageType);
							if (checkResistance)
							{
								continue;		
							}
						}
					}
					_character.ChangeCharacterConditionTemporarily(typedDamage.ForcedCondition, typedDamage.ForcedConditionDuration, typedDamage.ResetControllerForces, typedDamage.DisableGravity);	
				}
			}
		}

		/// <summary>
		/// Goes through the resistance list and applies movement multipliers if needed
		/// </summary>
		/// <param name="typedDamages"></param>
		protected virtual void ComputeCharacterMovementMultipliers(List<TypedDamage> typedDamages)
		{
			if ((typedDamages == null) || (_character == null))
			{
				return;
			}

			foreach (TypedDamage typedDamage in typedDamages)
			{
				if (typedDamage.ApplyMovementMultiplier)
				{
					if (TargetDamageResistanceProcessor != null)
					{
						if (TargetDamageResistanceProcessor.isActiveAndEnabled)
						{
							bool checkResistance =
								TargetDamageResistanceProcessor.CheckPreventMovementModifier(typedDamage.AssociatedDamageType);
							if (checkResistance)
							{
								continue;		
							}
						}
					}

					_characterHorizontalMovement?.ApplyContextSpeedMultiplier(typedDamage.MovementMultiplier,typedDamage.MovementMultiplierDuration);
				}
			}

		}


		/// <summary>
		/// Called when the character gets health (from a stimpack for example)
		/// </summary>
		/// <param name="health">The health the character gets.</param>
		/// <param name="instigator">The thing that gives the character health.</param>
		public virtual void GetHealth(float health, GameObject instigator)
		{
			// this function adds health to the character's Health and prevents it to go above MaxHealth.
			if (MasterHealth != null)
			{
				MasterHealth.SetHealth(Mathf.Min (CurrentHealth + health,MaximumHealth), instigator);	
			}
			else
			{
				SetHealth(Mathf.Min (CurrentHealth + health,MaximumHealth), instigator);	
			}
			UpdateHealthBar(true);
		}

		/// <summary>
		/// Sets the health of the character to the one specified in parameters
		/// </summary>
		/// <param name="newHealth"></param>
		/// <param name="instigator"></param>
		public virtual void SetHealth(float newHealth, GameObject instigator)
		{
			CurrentHealth = Mathf.Min(newHealth, MaximumHealth);
			UpdateHealthBar(false);
			HealthChangeEvent.Trigger(this, newHealth);
		}

		/// <summary>
		/// Resets the character's health to its max value
		/// </summary>
		public virtual void ResetHealthToMaxHealth()
		{
			CurrentHealth = MaximumHealth;
			UpdateHealthBar(false);
			HealthChangeEvent.Trigger(this, CurrentHealth);
		}

		private void UpdateSpriteScale()
		{
			if (spriteObject != null)
			{
				// 计算血量百分比 (0-1之间)
				float healthPercent = CurrentHealth / MaximumHealth;

				// 根据血量百分比计算缩放值
				float newScale = Mathf.Lerp(2, 4, healthPercent);

				// 设置新的缩放值
				spriteObject.transform.localScale = new Vector3(newScale, newScale, 1f);
			}
		}

		/// <summary>
		/// Updates the character's health bar progress.
		/// </summary>
		public virtual void UpdateHealthBar(bool show)
		{
			if (_healthBar != null)
			{
				_healthBar.UpdateBar(CurrentHealth, 0f, MaximumHealth, show);
			}

			if (_character != null)
			{
				if (_character.CharacterType == Character.CharacterTypes.Player)
				{
					// We update the health bar
					if (GUIManager.HasInstance)
					{
						GUIManager.Instance.UpdateHealthBar(CurrentHealth, 0f, MaximumHealth, _character.PlayerID);
					}
				}
			}
			UpdateSpriteScale();
		}

		/// <summary>
		/// Prevents the character from taking any damage
		/// </summary>
		public virtual void DamageDisabled()
		{
			TemporarilyInvulnerable = true;
		}

		/// <summary>
		/// Allows the character to take damage
		/// </summary>
		public virtual void DamageEnabled()
		{
			TemporarilyInvulnerable = false;
		}

		/// <summary>
		/// Prevents the character from taking any damage
		/// </summary>
		public virtual void EnablePostDamageInvulnerability()
		{
			PostDamageInvulnerable = true;
		}

		/// <summary>
		/// Allows the character to take damage
		/// </summary>
		public virtual void DisablePostDamageInvulnerability()
		{
			PostDamageInvulnerable = false;
		}

		/// <summary>
		/// Allows the character to take damage
		/// </summary>
		public virtual IEnumerator DisablePostDamageInvulnerability(float delay)
		{
			yield return MMCoroutine.WaitFor(delay);
			PostDamageInvulnerable = false;
		}

		/// <summary>
		/// makes the character able to take damage again after the specified delay
		/// </summary>
		/// <returns>The layer collision.</returns>
		public virtual IEnumerator DamageEnabled(float delay)
		{
			yield return MMCoroutine.WaitFor(delay);
			TemporarilyInvulnerable = false;
		}

		/// <summary>
		/// When the object is enabled (on respawn for example), we restore its initial health levels
		/// </summary>
		protected virtual void OnEnable()
		{
			if ((_characterPersistence != null) && (_characterPersistence.Initialized))
			{
				UpdateHealthBar(false);
				return;
			}

			InitializeCurrentHealth();
			DamageEnabled();
			DisablePostDamageInvulnerability();
			UpdateHealthBar(false);
			this.MMEventStartListening<HealthDeathEvent>();
		}

		/// <summary>
		/// Cancels all running invokes on disable
		/// </summary>
		protected virtual void OnDisable()
		{
			CancelInvoke();
			this.MMEventStopListening<HealthDeathEvent>();
		}

		public void OnMMEvent(HealthDeathEvent deathEvent)
		{
			if (KillOnMasterHealthDeath && (deathEvent.AffectedHealth == MasterHealth))
			{
				Kill();
			}
		}
	}
}
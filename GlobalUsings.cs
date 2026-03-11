// GlobalUsings.cs — Project-wide using directives
//
// In C# 10+, "global using" means the namespace is automatically available in every
// .cs file in this project. You don't need to add individual "using" statements at the
// top of each file. This keeps the code cleaner and avoids repetitive boilerplate.
//
// These are grouped by the library they come from:

// ── ACE.Common ──────────────────────────────────────────────────────────────
// Core utilities and extension methods shared across the ACE server.
global using ACE.Common;
global using ACE.Common.Extensions;

// ── ACE.Database ────────────────────────────────────────────────────────────
// Database model types for the shard DB (player/character data).
global using ACE.Database.Models.Shard;

// ── ACE.DatLoader ───────────────────────────────────────────────────────────
// Reads data from the game's .dat files (client art, spells, etc.)
global using ACE.DatLoader;
global using ACE.DatLoader.Entity.AnimationHooks;

// ── ACE.Entity ──────────────────────────────────────────────────────────────
// Core game entity types, enums, and property definitions.
global using ACE.Entity;
global using ACE.Entity.Enum;
global using ACE.Entity.Enum.Properties;
global using ACE.Entity.Models;

// ── ACE.Server ──────────────────────────────────────────────────────────────
// Server-side game logic: commands, world objects, factories, managers, networking.
global using ACE.Server.Command;
global using ACE.Server.Entity;
global using ACE.Server.Entity.Actions;
global using ACE.Server.Factories;
global using ACE.Server.Factories.Enum;
global using ACE.Server.Managers;
global using ACE.Server.Mods;
global using ACE.Server.Network.GameEvent.Events;
global using ACE.Server.Network.GameMessages.Messages;
global using ACE.Server.Network;
global using ACE.Server.Physics;
global using ACE.Server.WorldObjects.Entity;
global using ACE.Server.WorldObjects;

// ── REALM support (optional) ─────────────────────────────────────────────────
// Only compiled when the REALM build symbol is defined. This adds compatibility
// with ACE-Realm (a fork of ACE that supports instanced/realm-based gameplay).
// The type aliases below let the rest of the code use "Session" and "Position"
// without caring which fork of ACE is running.
#if REALM
global using ACE.Server.Realms;
//global using ACE.Database.Models.World;
global using Session = ACE.Server.Network.ISession;
global using BinaryWriter = ACE.Server.Network.GameMessages.RealmsBinaryWriter;
global using Position = ACE.Server.Realms.InstancedPosition;
#endif

// ── Harmony ──────────────────────────────────────────────────────────────────
// Harmony is the library that lets us intercept (patch) ACE's methods at runtime
// without editing ACE's source code.
global using HarmonyLib;

// ── System libraries ─────────────────────────────────────────────────────────
global using System.Diagnostics;            // Stopwatch (for timing code)
global using System.Numerics;               // Vector3, Quaternion (for positions/math)
global using System.Reflection;             // Reflection API (inspect types at runtime)
global using System.Text;                   // StringBuilder (efficient string building)
global using System.Text.Encodings.Web;
global using System.Text.Json;              // JSON serialization (for Settings.json)
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions; // Regex (used by StringRequirement)

// ── ACE.Shared ───────────────────────────────────────────────────────────────
// ACE.BaseMod's helper library — provides BasicMod, BasicPatch, FakeProperty, and
// a rich set of extension methods for Player, Creature, WorldObject, etc.
global using ACE.Shared;
global using ACE.Shared.Helpers;
global using ACE.Shared.Mods;

// ── AutoLoot internal ────────────────────────────────────────────────────────
// Makes the VTClassic (VirindiTank-compatible) loot library available everywhere
// in this project without needing a "using AutoLoot.Lib.VTClassic;" in each file.
global using AutoLoot.Lib.VTClassic;

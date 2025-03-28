# Project C: The Clouds  
**Version:** 0.2.1.1 | **Stage:** Prototype 
## Whole project: [TheGravity](https://thegravity.ru) & [TheClouds](https://thegravity.ru/project-c/)

---
**no marketing/bullshit/tech-heavy/sound sections**
---

## 1. Introduction  
"Project-C" is a sci-fi flight simulator focused on trading and interaction in an alternate reality based on the book *Integral Pyavitsa*.

**Setting:**  
- **1930s:** Meteorites carrying the substance "Mezy" and metal "Antigravium" poisoned Earth’s lower atmosphere. Humanity built an artificial barrier to contain the toxins.  
- **2050s:** Civilizations thrive above the clouds using antigravity platforms and Mezy-powered generators. Key settlements exist on mountaintops (names from the book) and flying platforms.  

**Technologies:**  
- **Antigravium:** A metal that disrupts gravity when electrified. Used in ships with propeller engines and antigravity modules.  
- **Mezy Generators:** Power cities but require frequent maintenance.  

**Game Focus:**  
- **Exploration:** Vast world with locations inspired by real mountains (Everest, Kilimanjaro) and artificial platforms.  
- **Trade & Logistics:** Dynamic economy with cargo transport, smuggling, and resource management.  
- **Conflict:** Battles against pirates using pneumatic weapons (harpoons, nets).  
- **Progression:** Ship customization, faction interactions, uncovering secrets of Mezy/Antigravium.  

---

## 2. Visual Style: Sci-Fi + Ghibli Elements  
**Architecture:**  
- **Platforms:** Industrial designs (steel, pipes) blended with Ghibli-esque curves — domes with stained glass, intricate bridges.  
- **Mountain Bases:** Cliffside settlements with terraces and waterfalls.  

**Sky & Clouds:**  
- **Ghibli Aesthetic:** Soft sunset gradients, volumetric glowing clouds.  
- **Realistic Cloud Layers** stratified by altitude.  
- **Tech Elements:** Cracked artificial barrier with lightning, patrol drones.  

**Ships:**  
Utilitarian designs with propellers/antigrav modules + smooth contours, neon accents, gradient paint.  

---

## 3. Core Mechanics  
### Technical Layer: Controls & Gameplay (PC)  
#### 1. Core Control Principles  
Arcade-style controls prioritize accessibility and fluidity. Simplified physics: no wind or complex aerodynamics.  

**1.1. Third-Person View**  
- **Ship:** Camera follows from behind/above for environmental awareness.  
- **Character:** Over-the-shoulder view when on foot, with manual camera rotation.  

#### 2. Ship Controls (Keyboard & Mouse)  
| Action                  | Key/Mouse Input        | Description                                                                 |
|-------------------------|------------------------|-----------------------------------------------------------------------------|
| Forward Thrust          | W                      | Smooth acceleration. Max speed in 2-3 seconds.                             |
| Reverse Thrust          | S                      | Deceleration or slow backward movement.                                    |
| Turn Left/Right         | A/D                    | Inertia-based rotation. Sharp turns cause slight drift.                    |
| Ascend                  | Space                  | Activates antigravium for vertical lift.                                   |
| Descend                 | Ctrl                   | Gradual altitude drop.                                                     |
| Turbo Boost             | Shift                  | Short burst forward. 5-second cooldown.                                    |
| Fire (Harpoons/Nets)    | Left Mouse Button      | Limited ammo. Reload at docks.                                             |
| Interact (Dock)         | E                      | Auto-land on nearest platform.                                             |
| Map/Menu                | Tab                    | Pause to access map, inventory, and settings.                              |

**Features:**  
- **Auto-Stabilization:** Ship auto-levels when releasing controls.  
- **Camera Control:** Hold right mouse button to freely rotate the camera.  
- **Hints:** On-screen prompts for key actions (docking, turbo).  

#### 3. On-Foot Controls  
| Action                  | Key/Mouse Input        | Description                                      |
|-------------------------|------------------------|--------------------------------------------------|
| Movement                | W/A/S/D                | Smooth multidirectional movement.               |
| NPC Interaction         | E                      | Dialogues, quests, trading.                     |
| Jump                    | Space                  | Short hop with auto-landing.                    |
| Sprint                  | Shift                  | Faster movement in settlements.                 |
| Environment Scan        | Right Mouse + Move     | Rotate camera to inspect surroundings.          |
| Inventory/Settings      | I                      | Access gear, map, and upgrades.                 |

#### 4. Control Customization  
- **Rebind Keys:** Fully customizable input mapping.  
- **Mouse Sensitivity:** Adjust camera/ship rotation speed.  

#### 5. Tutorial  
- **Integrated Tutorial:** Step-by-step guides for basics (flight, docking, trade). Hints disappear after first use.  
- **Dynamic Tips:** Contextual advice for new scenarios (e.g., entering turbulence).  

### 3.1. Flight & Physics  
- **Altitude Effects:** Temperature, pressure, and turbulence vary with height.  
- **Antigravium Mechanics:** Reduces ship mass; propellers provide thrust.  
- **Mezy Turbulence:** Hazardous zones with high Mezy concentration.  

- **Customization:**  
  - **Functional:** Upgrade cargo capacity, speed, armor.  
  - **Cosmetic:** Gradient paints, neon lights, decorative parts (e.g., "feather" stabilizers).  
  - **Repairs:** Fix components at docks or perform emergency mid-flight repairs.  

### 3.2. Trade & Economy  
- **Dynamic Pricing:** Prices shift based on location/events (disasters, pirate raids).  
- **Contracts:** Delivery missions, urgent orders, smuggling.  
- **Black Market:** Trade rare resources and tech.  

### 3.3. Piracy  
- **Pirate Tactics:** Harpoons to immobilize ships, nets to restrict movement.  
- **Countermeasures:** Evasive maneuvers or exploiting turbulence zones.  

### 3.4. Factions  
- **Impact:**  
  - **Story:** Unique quests revealing world lore.  
  - **Access:** Exclusive tech, locations, and resources via reputation.  

### 3.5. Exploration  
- **Abandoned Platforms:** Loot artifacts (blueprints, logs) amid traps and toxins.  
- **Dynamic Events:** Antigravity anomalies, electric storms.  

---

## 4. On-Foot Gameplay  
### 4.1. Settlements & Stations  
**Locations (from the book):**  
- **Trade Hubs:** Multi-level markets with automated cranes/conveyors.  
- **Engineering Docks:** Robotic repair bays.  
- **Administrative Centers:** Platform resource management zones.  

**NPCs:**  
- **Mechanics:** Offer quests for rare components.  
- **Contractors:** Provide special cargo missions.  
- **Scientists:** Share Mezy/Antigravium lore via dialogue.  

**Interactions:**  
- **System Management:** Mini-games to balance platform energy grids.  
- **Negotiations:** Influence prices through trader dialogues.  

### 4.2. Ruin Exploration  
- **Descent Below the Barrier:** Limited time due to toxicity. Hunt pre-collapse artifacts (documents, tech).  
- **Dangers:** Collapsing structures, pirate traps.  

### 4.3. Social Systems  
- **Settlement Quests:**  
  - **Investigations:** Track missing cargo or sabotage.  
  - **Technical Tasks:** Repair infrastructure (e.g., stabilize generators).  
- **Mini-Games:**  
  - **System Repairs:** Tool-based repair mechanics.  
  - **Trade Auctions:** Strategic bidding for rare resources.  

---

## 5. Core Loop  
1. **Accept Mission:** Choose contracts (trade, exploration, piracy).  
2. **Execute:**  
   - Pilot your ship.  
   - Trade, repair, combat.  
   - Explore stations/ruins on foot.  
3. **Progress:**  
   - Earn credits/resources.  
   - Upgrade ships/build faction rep.  
   - Unlock new missions/locations.  

**Example:**  
Player delivers cargo to a mountain base → explores station for artifact quest → finds it in a derelict platform → upgrades ship → unlocks faction missions.  

---

## 6. Unique Selling Points  
- **Ghibli x Sci-Fi:** Gradient-heavy visuals meets industrial realism.  
- **Depth:** Trade, customization, factions, on-foot exploration.  
- **No Magic:** All tech grounded in Mezy/Antigravium.  

---

## 7. Co-Op Mode  
Team up with players for shared missions, exploration, and threats.  

### 7.1. Key Features  
- **Shared Missions:**  
  - **Cargo Escort:** Defend transports from pirates.  
  - **Platform Assault:** Clear pirate bases (roles: pilot, gunner, engineer).  
  - **Ruin Exploration:** Split roles (pilot + explorer).  

- **Shared Progression:**  
  - Split credits/resources.  
  - Collaborative ship customization (merge parts for unique designs).  

- **Sync:**  
  - Seamless join/leave without interrupting gameplay.  
  - Shared inventory for critical quests (e.g., joint artifact delivery).  

### 7.2. Co-Op Mechanics  
- **Role Division:**  
  - **Pilot:** Navigate/evade.  
  - **Engineer:** Repair/optimize systems.  
  - **Gunner:** Control weapons (harpoons, nets).  

- **Co-Op Events:**  
  - **Antigravity Storms:** Require synchronized engine stabilization.  
  - **Pirate Raids:** Defend convoys in PvE.  

### 7.3. Technical Implementation  
- **Networking:**  
  - Up to 4 players per session.  
  - Voice chat and marker system for coordination.  

- **Balance:**  
  - Increased difficulty in co-op.  
  - Exclusive rewards (e.g., Ghibli-themed ship skins).  

### Co-Op Integration into Core Loop  
1. **Accept Mission:** Choose co-op missions or host a session.  
2. **Execute:**  
   - Shared ship control or role split.  
   - Coordinate via UI (mark objectives, share resources).  
3. **Progress:**  
   - Shared credits/resources.  
   - Unlock team-exclusive upgrades.  

**Example:**  
Players A & B deliver Antigravium through a danger zone.  
Player A pilots; Player B repairs during turbulence.  
Success grants double rewards and a co-op-exclusive skin.  
**Repository:** [GitHub Link](https://github.com/boozzeeboom/project-c)  
**Contact:** [@indeed174](https://t.me/indeed174).

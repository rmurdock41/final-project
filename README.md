# Amaterasu's Brush: Interactive Nature Restoration

## CIS 566 Final Project Design Document

Muqiao Lei

---

![See attached pipeline diagram](imgs/cover.png)

---

## Introduction

This project extends the Mix and Jam Celestial Brush project. The original project implements a gesture recognition system, and I will add actual visual effects on top of it.

I will use Houdini to create assets like tree blooming and grass growth, then import them into Unity so gestures can trigger these effects. I will also implement a day/night transition system.

---

## Goal

Extend the existing Mix and Jam Celestial Brush project with the following features:

1. **Day/Night Control** - Draw sun/moon to transition sky colors and lighting
2. **Tree Blooming** - Draw on dead trees to trigger blooming and leaf growth effects

---

## Inspirations and References

**Primary Reference - Mix and Jam Celestial Brush:**

- GitHub: https://github.com/mixandjam/Okami-Celestial-Brush
- Video: https://www.youtube.com/watch?v=yuQXeaYBuuM
- **Usage**: Foundation for gesture recognition and input handling

**Original Game - Okami:**

- Bloom technique: reviving dead trees
- Sunrise technique: controlling day/night

**Houdini Technical References:**

- L-system for plant generation
- Animation and growth effects

---

## Specifications

### Core Features:

**1. Day/Night System**

- Recognize sun/moon gestures
- Sky color transitions
- Lighting changes

**2. Tree Blooming System**

- Detect circle gesture on dead trees
- Play tree blooming animation:
  - Dead tree turns green
  - Leaves grow
  - Flowers bloom

---

## Techniques

### Workflow:

**Houdini:**

- Create tree/plant models
- Use L-system or other tools to generate plants
- Create blooming/growing animations
- Export to Unity

**Unity:**

- Import Houdini assets
- Connect gesture recognition to effect triggers
- Handle day/night lighting changes

---

## Design

![See attached pipeline diagram](imgs/designDoc.png)

**Workflow:**

Houdini Production:

- Tree modeling → L-System → Scatter leaves/flowers → Bloom animation → Export FBX
- Grass modeling → Scatter/Copy → Wind animation → Export FBX

Unity Integration:

- Import assets → Setup materials/animation/lighting
- Connect to gesture framework
- Implement three systems: DayNightSystem, TreeBloomSystem, GrassSystem
- Effect Manager for unified control
- Final Demo

---

## Implemented Feature

### 1. Day/Night System

**Implementation Details:**

- **SkyboxController Script**: Controls smooth transitions between day and night skyboxes

- **Key Features:**
  
  - Uses two separate skybox materials (day/night)
  - Smooth interpolation of the following properties:
    - Sun disc color (`_SunDiscColor`)
    - Sun halo color (`_SunHaloColor`)
    - Horizon line color (`_HorizonLineColor`)
    - Sky gradient colors (top and bottom)
  - Dynamic directional light intensity adjustment (dims to 0.1 at night, restores to maximum during day)
  - Real-time global illumination updates (`DynamicGI.UpdateEnvironment()`)

- **Control Methods:**
  
  - Press `D` key: Switch to day
  - Press `N` key: Switch to night
  - Customizable transition speed parameter

- **Technical Implementation:**
  
  - Uses material instances to avoid modifying original assets
  - Lerp interpolation for smooth transition effects
  - Memory management: Cleans up material instances in `OnDestroy`

---

### 2. Ink Particle Effects System

**Implementation Details:**

- **InkToParticles Script**: Converts brush strokes into particle effects inspired by Okami
- **ParticleFadeOut Script**: Handles individual particle fade-out and animation behavior

**Key Features:**

- **Brush Stroke to Particle Conversion:**
  
  - Samples points along LineRenderer path based on configurable density (`particlesPerUnit`)
  - Gradually converts ink strokes into particles with staggered timing
  - Validates all position data to prevent NaN errors

- **Particle Behavior:**
  
  - Random positioning offset for natural spread effect
  - Camera-facing orientation with random rotation
  - Random scale variation (0.3-0.8x) for visual variety
  - Customizable particle color (default: orange `Color(1f, 0.5f, 0f)`)

- **Animation System:**
  
  - **Two-phase animation:**
    1. Particle generation phase (0.4s): Particles spawn progressively along stroke
    2. Line fade-out phase (1.0s): Original line gradually disappears from end to start
  - **Particle fade-out effects:**
    - Alpha fade over customizable duration
    - Scale reduction animation
    - Continuous rotation (100-300 degrees/second)
    - Staggered fade delays for cascading effect

- **Technical Implementation:**
  
  - Uses coroutines for smooth gradual conversion
  - Material instancing to avoid asset modification
  - Custom render queue ordering (particles at 3100, line at 2000)
  - Proper transparency blending setup (SrcAlpha/OneMinusSrcAlpha)
  - Memory management: Automatic cleanup of line and particles after animation

- **Visual Polish:**
  
  - Particles positioned slightly toward camera for depth
  - Line gradually reveals while particles are generated
  - Smooth transition from ink stroke to floating particles

---

### 3. Celestial Object System (Sun/Moon)

**Implementation Details:**

- **SunAmaterasu Script**: Generates and controls sun effects with day/night transition
- **MoonAmaterasu Script**: Generates and controls moon effects with day/night transition
- **TextureGenerator Script**: Procedurally generates Okami-style sun ray textures

**Sun System Features:**

- **Multi-layer Visual Effects:**
  
  - Sun disc layer
  - Sun rays layer with 12 rotating beams
  - Uses Okami's signature vermillion color (`Color(1f, 0.25f, 0.1f)`)

- **Animation Sequence:**
  
  1. Sun disc fades in (1 second)
  2. White flash screen effect
  3. Triggers sky transition to day
  4. Rays layer fades in (0.5 seconds)
  5. Rays continuously rotate (30 degrees/second)
  6. Displays for 2.5 seconds before disappearing
  7. Disappearance animation: Rays fade out first, disc fades out after 0.2 second delay

- **Screen Flash Effect:**
  
  - Creates fullscreen white canvas overlay
  - Fades from fully opaque to transparent (0.5 seconds)
  - Uses DOTween for smooth transitions
  - Automatic cleanup after animation

**Moon System Features:**

- **Minimalist Design:**
  
  - Single moon object layer
  - Quick fade-in animation (0.2 seconds)
  - Triggers sky transition to night
  - Brief display before fading out (configurable `displayTime`)

- **Animation Sequence:**
  
  1. Moon quickly fades in
  2. Triggers sky transition to night
  3. Displays for set duration (default 0.5 seconds)
  4. Fades out and destroys (1 second)

**Procedural Texture Generation:**

- **TextureGenerator Editor Tool:**
  - Adds "Tools/Generate Okami Sun" button to Unity menu
  - Generates 512x512 resolution texture
  - Procedurally creates 12-beam ray pattern
  - Uses trigonometric functions (`Cos(angle * 12)`) for evenly distributed beams
  - Cel-shading style with sharp edges
  - Hollow center design (leaves space for sun disc)
  - Auto-saves as PNG file to Assets directory

**Technical Implementation:**

- Uses DOTween plugin for smooth fade animations
- Coroutines control complex animation sequences
- Dynamic material alpha channel for transparency control
- Integration with SkyboxController for global day/night transitions
- Memory management: Automatic object destruction after animation completion

---

### 4. Plant Growth Animation System

**Implementation Details:**

- **PlantAnimationPlayer Script**: Controls Alembic animation playback and object lifecycle
- **Houdini Asset Workflow**: Creates procedural plant growth animations
  
  link to this .abc file (need to put this under asset folder) https://drive.google.com/file/d/1Gd7eytmGq8QGVedfsjnjZ6YcEPdpaOYz/view?usp=drive_link

**Houdini Production Pipeline:**

- **Plant Modeling:**
  
  - Plants separated into two components: branches and leaves
  - Individual curl-to-unfold growth animations for each component
  - Exported as Alembic (.abc) format files

- **Animation Design:**
  
  - Curled state as starting frame
  - Gradual unfurling to fully expanded state
  - Maintains natural, fluid motion

**Unity Integration:**

- **Asset Import:**
  
  - Import Alembic files into Unity
  - Uses Unity's Alembic Importer package
  - Configured as Prefabs for reusability

- **Animation Playback Control:**
  
  - Uses `AlembicStreamPlayer` component
  - Starts playback from timeline origin (`CurrentTime = 0`)
  - Real-time animation time updates for full sequence playback

**Animation Sequence:**

1. **Growth Phase:**
   
   - Plays complete Alembic animation sequence
   - Animation duration determined by imported Alembic file
   - Uses coroutines for frame-by-frame time updates

2. **Display Phase:**
   
   - Holds for 3 seconds after animation completes
   - Showcases fully expanded plant state

3. **Disappearance Phase:**
   
   - Shrink animation (2 seconds)
   - Uses `Vector3.Lerp` for smooth scale interpolation
   - Scales from original size to zero
   - Automatic object destruction upon completion

**Technical Implementation:**

- Coroutines manage complex multi-stage animation sequences
- Dynamic time control enables Alembic animation playback
- Smooth scale interpolation prevents abrupt disappearance
- Configurable shrink animation duration (`shrinkDuration`)
- Automatic memory management and object cleanup

---

### 5. Gesture Recognition and Integration System

**Implementation Details:**

- **Demo Script**: Core gesture recognition and effect trigger manager
- **Based on PDollar Gesture Recognizer**: Uses point cloud recognition algorithm for gesture matching

**Gesture Recognition Features:**

- **Supported Gesture Types:**
  
  - **Sun Gesture (Sun/Cherrybomb)**: Circular gesture - Triggers sun generation and day transition
  - **Moon Gesture (Moon)**: Crescent shape - Triggers moon generation and night transition
  - **Plant Gesture (Plant)**: Custom gesture - Generates plants on ground
  - **Line Gesture (Horizontal Line/Line)**: Horizontal slash - Cuts trees (original feature)
  - **Bomb Gesture (Cherrybomb)**: Circular gesture on objects - Generates explosion effect (original feature)

- **Gesture Drawing System:**
  
  - Real-time LineRenderer tracking mouse/touch input
  - Multi-touch and multi-stroke support
  - Dynamic vertex count and position updates
  - Cross-platform support (PC, Android, iOS)

**Sun Gesture Effects:**

- **Trigger Conditions:**
  
  - Recognizes "cherrybomb" or "sun" gesture
  - No collision at gesture center (drawn in open space)
  - Recognition confidence threshold > 0.75

- **Effect Sequence:**
  
  1. Ink stroke converts to orange particle effects
  2. Sun spawns 20 units behind gesture center
  3. Sun faces camera
  4. Triggers SkyboxController to switch to day
  5. Cleans up strokes and resets recognition state

**Moon Gesture Effects:**

- **Trigger Conditions:**
  
  - Recognizes "moon" gesture
  - No collision at gesture center
  - Recognition confidence threshold > 0.75

- **Effect Sequence:**
  
  1. Ink stroke converts to blue particle effects (`Color(0.5f, 0.5f, 1f)`)
  2. Moon spawns 20 units behind gesture center
  3. Moon faces camera
  4. Triggers SkyboxController to switch to night
  5. Cleans up strokes and resets recognition state

**Plant Gesture Effects:**

- **Trigger Conditions:**
  
  - Recognizes "plant" gesture
  - Raycast from stroke start point detects ground
  - Recognition confidence threshold > 0.75

- **Effect Sequence:**
  
  1. Plant spawns at raycast hit point (slightly lowered by 0.2 units)
  2. Automatically plays Alembic growth animation
  3. Cleans up strokes and resets recognition state

**Bomb Gesture Effects (Original Feature):**

- **Trigger Conditions:**
  
  - Recognizes "cherrybomb" or "sun" gesture
  - Collision detected at gesture center (drawn on objects)
  - Recognition confidence threshold > 0.75

- **Effect Sequence:**
  
  1. Sphere explosion spawns at gesture center
  2. Triggers camera shake effect
  3. Sphere features elastic scale animation (DOTween OutBack)
  4. Cleans up strokes and resets recognition state

**Gesture Recognition Technology:**

- **PDollar Point Cloud Recognition Algorithm:**
  
  - Converts drawn strokes into point collections
  - Matches against pre-trained gesture set
  - Returns best match gesture and confidence score
  - Recognition below 0.75 confidence is ignored

- **Training Set Management:**
  
  - Pre-loads built-in gesture set (Resources/GestureSet/)
  - Supports user-defined custom gestures
  - Dynamically adds new gestures to training set
  - Gesture data stored in XML format

**Ink Particle Integration:**

- Sun gesture uses orange particles (default color)
- Moon gesture uses light blue particles
- Particle density: 3 particles per unit
- Fade duration: 0.7 seconds
- Automatic line object cleanup after conversion

**Collision Detection and Spatial Positioning:**

- **Raycast System:**
  
  - Casts ray from gesture center toward camera forward direction
  - Detects if scene objects are hit
  - Sun/moon require open space to trigger
  - Plants require ground hit to spawn
  - Bombs require object hit to trigger

- **Camera Space Calculations:**
  
  - Celestial objects placed at appropriate position in camera view
  - 20 units away from gesture center (away from camera direction)
  - Always face camera to maintain front visibility

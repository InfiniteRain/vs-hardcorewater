Hardcore Water: Transport Edition (Evolved)
=================

Fork Information
----------------

This is a fork of the original Hardcore Water: Transport Edition mod to 1.22, with the following features:

* Backwards compatible with the old version of the mod, meaning that it's safe to upgrade existing worlds that use the mod to 1.22.
* The stone aqueducts can now be crafted out of every type of rock:
  * NEW: Andesite
  * NEW: Chalk
  * NEW: Chert
  * NEW: Conglomerate
  * NEW: Claystone
  * NEW: Shale
  * NEW: Peridotite
  * NEW: Phyllite
  * NEW: Slate
  * NEW: Bauxite
  * Limestone
  * Sandstone
  * Basalt
  * Granite
* Forked and upgraded fully by hand, with no involvment of AI. Even translations for the new aqueduct blocks were 
sourced from a native speaker.

Overview
--------

Originally, this mod prevented water from being placed by buckets; now that source bucket prevention is a vanilla world option with VS 1.20.x, this mod is more focused on transportation methods to move water around.

This mod currently adds:

* Aqueduct sections
  * Can be made with 3 bricks and 1 mortar (creates 3 sections), or a debarked log and resin (creates 1 section, requires hammer and chisel).
  * One section must be connected to a source block to propagate water along a length of sections.
  * Enclosed aqueducts which cannot feed aqueducts from the side, but can be used in room walls without affecting room integrity. 

Note that aqueducts can feed other aqueducts when placed orthogonal to each other, but only one-way. The source aqueduct in this arrangement will have smaller openings.


Config Settings (`VintageStoryData/ModConfig/HardcoreWater.json`)
--------

* `AqueductUpdateFrequencySeconds`: Sets how often aqueducts are allowed to update, in seconds; defaults to `0.75`.


Future Plans
--------

* Mechanical screw pumps and/or water-lifting devices; moves any adjacent water upward when powered; helpful for when water is needed at a higher elevation than nearby source blocks.


Known Issues
--------

* Visual glitches can sometimes occur when adjacent to filled aqueducts and the camera is turned.
* Water will not flow out of an aqueduct section downwards unless a full block is placed below the end section.
* No compatibility yet with Wildcraft Trees.

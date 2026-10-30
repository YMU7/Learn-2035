# Marker folder (no sample files here)

The real XRI 3.3.0 Starter Assets used by this project are the author-modified copy in
`Assets/XRTKSamples/XR Interaction Toolkit/3.3.0/Starter Assets/`.

XRI project validation (e.g. the Spatial Keyboard sample check) only looks for samples under
`Assets/Samples/<Package>/<Version>/<Sample>`. Without this folder it reports Starter Assets as
missing, opens the Project Validation window on every script reload / Play, and its "Fix" imports a
second stock copy that breaks compilation (duplicate assembly names).

Keep this folder. Do not import the Starter Assets sample from the Package Manager.

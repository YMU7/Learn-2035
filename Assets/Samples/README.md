# Samples (intentionally empty)

XR Interaction Toolkit's project validation expects this folder to exist, otherwise it logs
"Could not find Samples directory (Assets\Samples)" on every domain reload.

Do NOT import XRI or XR Hands samples from the Package Manager into this folder.
This project uses the author-modified copies in `Assets/XRTKSamples/`; a second import creates
duplicate assembly names ("Assembly with name ... already exists") and blocks compilation.

`XR Interaction Toolkit/3.3.0/Starter Assets/` here is only a marker folder (see its README) so the
validation check passes; it contains no sample files.

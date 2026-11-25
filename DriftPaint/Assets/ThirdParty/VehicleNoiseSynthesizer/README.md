# Vehicle Noise Synthesizer (VNS 1.9)

Sintetizador de motor por bancos de grabaciones según RPM y carga, de ATG Simulator.

- Origen: https://github.com/ATG-Simulator/VehicleNoiseSynthesizer
- Licencia: MIT (ver `LICENSE.txt`). Copyright (c) 2023 ATG Simulator.
- Documentación: https://docs.atg-simulator.com/vehiclenoisesimulator

## Qué se trajo

- `Runtime/VehicleNoiseSynthesizer.cs` y `Runtime/VehicleNoiseBatchManagerLifetime.cs` sin cambios.
- `Editor/VehicleNoiseSynthesizerEditor.cs` sin cambios.

## Qué se dejó afuera

- Escenas de ejemplo y las integraciones con NWH Vehicle Physics y Dynamic Water Physics, que necesitan assets pagos.
- Los clips del V12 de ejemplo (cedidos por Street Shift Game sin licencia explícita).

Los bancos de DriftPaint se generan en el proyecto (`Assets/Audio/Engines`) y los usa `CarEngineAudio`.
Para actualizar el paquete, reemplazar los tres scripts y revisar que `CarEngineAudio` siga configurando los mismos campos.

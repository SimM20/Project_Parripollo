# Lista de trabajos: (fuente relativa a Assets/Sprites, modo, ancho, alto, PPU, [referencias extra], nota)
# El PNG nuevo va a Assets/Sprites/Pixel480/<misma ruta>.png
AA = "Arte Aseprite/"
EXP = "@EXPORT/"   # .aseprite exportados por el MCP de Aseprite
J = []
def job(src, mode, w, h, ppu, refs=None, note="", crop=None, out=None, border=None, outline=0.35, parts=None):
    J.append(dict(src=src, mode=mode, w=w, h=h, ppu=ppu, refs=refs or [], note=note, crop=crop, out=out, border=border, outline=outline, parts=parts))

# --- Mundo (SpriteRenderer): tamaño = como se ve hoy, PPU = 25 x escala del objeto -> 1 px de arte = 1 px de pantalla
for n in ["Carbon Apagado", "Carbon Ceniza", "Carbon Prendido (Fase 1)", "Carbon Prendido (Fase 2)", "Carbon Prendido (Fase 3)"]:
    job(AA+"Carbon/"+n+".png", "pixel", 18, 18, 8.75,
        refs=[AA+"Carbon/Carbon Apagado.aseprite"] if n == "Carbon Apagado" else [], note="carbon en la parrilla (escala 0,35)")
for i in range(1, 8):
    job(AA+f"Clientes/Personaje POV {i}.png", "pixel", 63, 105, 21, note="cliente (escala 0,84)")
cortes = {"Choripan": ["Choripan Crudo","Choripan Hecho","Choripan Jugoso","Choripan Muy Hecho","Choripan Pasado"],
          "Chorizo": [f"Chorizo {s}{r}" for s in ["Crudo","Hecho","Jugoso","Muy Hecho","Pasado"] for r in [""," 90"]],
          "Paty": [f"Paty {s}" for s in ["Crudo","Hecho","Jugoso","Muy Hecho","Pasado"]],
          "Tira de Asado": [f"Tira de Asado {s}" for s in ["Cruda","Hecha","Jugosa","Muy Hecha","Pasada"]],
          "Vacio": [f"Vacio {s}" for s in ["Crudo","Hecho","Jugoso","Muy Hecho","Pasado"]]}
for d, names in cortes.items():
    for n in names:
        job(AA+f"Cortes/{d}/{n}.png", "pixel", 25, 25, 25, note="corte en la parrilla (1/4)")
job(EXP+"Carne Generico.png", "pixel", 25, 25, 25, refs=[AA+"Cortes/Carne Generico.aseprite"], out=AA+"Cortes/Carne Generico.png", note="icono de la tienda")
# GrillView esta aplastado en X (0,81): el dibujo se reescala con la misma deformacion que se ve hoy y el objeto pasa a escala pareja
job("Parrilla 2.5.png", "hd", 311, 256, 25, note="parrilla (0,2025 x 0,25)")
job(AA+"Objetos de las Views/Tabla_correcta_2.png", "hd", 219, 113, 23.6148, note="tabla ToBuild")
job(AA+"Objetos de las Views/ChatGPT_Image_30_jul_2026_18_07_36.png", "hd", 134, 62, 7.45225, note="tabla de abajo")
job(AA+"Objetos de las Views/ChatGPT Image 1 sept 2026, 01_12_31 a.m..png", "hd", 85, 54, 5.23675, note="plato")
job(AA+"Objetos de las Views/MeatHolderParrilla.png", "hd", 117, 117, 23.6148, note="bandeja de carne (inactiva)")
job(AA+"Objetos de las Views/Boton Cambio de Grilla Variable1.png", "pixel", 51, 15, 15, note="boton carne/carbon", outline=1.1)
job(AA+"Objetos de las Views/Boton Cambio de Grilla Variable2.png", "pixel", 51, 15, 15, note="boton carne/carbon (otro estado)", outline=1.1)
job(EXP+"Tacho Trash Buildview.png", "pixel", 19, 24, 37.5, refs=[AA+"Objetos de las Views/Tacho Trash Buildview.aseprite"], out=AA+"Objetos de las Views/Tacho Trash Buildview.png", note="tacho")
job(AA+"Objetos de las Views/Freezer Horizontal.png", "pixel", 163, 96, 25, note="vista cooler (deprecada)")
job(AA+"Objetos de las Views/Coal Container.png", "pixel", 80, 96, 25, note="vista cooler (deprecada)")
job(AA+"Objetos de las Views/Tabla Metal (Cooler a Grill).png", "pixel", 50, 100, 25, note="vista cooler (deprecada)")
job("Radio.png", "hd", 54, 35, 3.45875, note="radio")
# Barra de coccion: MeatCookHoverBar ubica la aguja en pixeles del sprite, asi que cada parte se reduce a pixeles
# enteros (marco 7 -> 1, segmento 40 -> 8, separador 5 -> 1; alto: margen 8 -> 1, madera 6 -> 2, color 26 -> 5).
# Valores del prefab: firstSegmentX 1, segmentWidth 8, dividerWidth 1, segmentTopY 3, segmentHeight 5.
job(AA+"UI y Botones/Barra Coccion v4.png", "parts", 55, 11, 19.125, note="barra de coccion del hover",
    parts=([(7, 1)] + [p for k in range(6) for p in ([(40, 8)] + ([(5, 1)] if k < 5 else []))] + [(7, 1)],
           [(8, 1), (6, 2), (26, 5), (6, 2), (8, 1)]))
job(AA+"UI y Botones/Indicador de Progreso.png", "pixel", 3, 10, 19.125, note="indicador de la barra")
job("Barra/barra.png", "pixel", 25, 7, 12.5, note="barra de coccion sobre la carne")
job("Barra/barraFilled.png", "pixel", 25, 7, 12.5, note="barra de coccion sobre la carne")
job(EXP+"Asset panel lateral cortes.png", "pixel", 80, 47, 15.675, refs=[AA+"UI y Botones/Asset panel lateral cortes.aseprite"], out=AA+"UI y Botones/Asset panel lateral cortes.png", note="fondo de la reaccion del cliente")
job("FondoCicloDia/Paisaje.png", "pixel", 480, 203, 25, note="fondo")
job("FondoCicloDia/NubesCercanas.png", "pixel", 480, 38, 25, note="nubes (tiled)")
job("FondoCicloDia/NubesLejanas.png", "pixel", 480, 38, 25, note="nubes (tiled)")
for n, w, h in [("chimi",15,16),("ensalada",16,16),("ensaladahuevo",15,16),("panchori",16,15),("panham",19,17),("criolla",33,20),("papas",33,20)]:
    job(n+".png", "hd", w, h, 1.25, note="topping (panel de toppings, escala 0,05)")

# --- UI: todo se escalo x1/4 -> arte /4 con PPU 25 (los bordes 9-slice quedan igual que hoy)
for n in ["Boton Comenzar", "Boton Continuar", "Boton Tipo Terminar Jornada", "Barra Sin Bolita"]:
    job(AA+f"UI y Botones/{n}.png", "pixel", 100, 25, 25,
        refs=[AA+"UI y Botones/Barra Sin Bolita.aseprite"] if n == "Barra Sin Bolita" else [], note="UI 1/4")
job(AA+"UI y Botones/Bolita de la Barra.png", "pixel", 25, 25, 25, note="UI 1/4")
job(AA+"UI y Botones/Fondo para Textos Corto.png", "pixel", 75, 50, 25, note="UI 1/4")
job(AA+"UI y Botones/UI Contables.png", "pixel", 70, 19, 17.5, note="HUD (70 x 18,8)")
job("Pausa.png", "hd", 20, 19, 5.319, note="HUD")
job("volver.png", "hd", 20, 19, 6.192, note="HUD deshacer / pausa")
job("cajaregistradora.png", "hd", 20, 20, 5.291, note="pausa")
job("puerta.png", "hd", 20, 20, 5.780, note="pausa")
for n, w in [("Clientes", 338), ("moneda", 294), ("sol", 410)]:
    job(n+".png", "hd", 15, 15, round(100*15/w, 3), note="icono HUD 15x15")
job("AngryCustomer.png", "hd", 49, 45, 3.735, note="popup de derrota (preserve aspect)")
job("PauseMenubg.png", "hd", 480, 270, 17.44, note="fondo pausa / tienda (pantalla completa)")
# Tutorial nuevo (TutorialHint): carteles con pixelsPerUnitMultiplier 2 -> arte /8 con PPU 12,5; glifos de 10 px de alto
job("UI/Tutorial/Cartel PH.png", "hd", 16, 16, 12.5, border=(5,5,5,5), note="cartel del tutorial (9-slice)")
job("UI/Tutorial/Tecla PH.png", "hd", 8, 10, 12.5, border=(3,3,3,2), note="tecla (9-slice)")
job("UI/Tutorial/Flecha Cartel PH.png", "hd", 6, 3, 12.5, note="flecha del cartel")
for n, w in [("Mouse Click PH", 7), ("Mouse Click Derecho PH", 7), ("Mouse Hover PH", 7), ("Mouse Arrastrar PH", 15)]:
    job(f"UI/Tutorial/{n}.png", "hd", w, 10, 12.5, note="glifo 10 px")
job("UI/Tutorial/Boton Joystick PH.png", "hd", 10, 10, 12.5, note="glifo 10 px")
for n in ["LetraE", "LetraQ", "LetraW"]:
    job(AA+f"UI y Botones/{n}.png", "hd", 9, 10, 2.0, note="glifo 10 px")

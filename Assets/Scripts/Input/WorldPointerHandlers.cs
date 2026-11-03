/// <summary>
/// Eventos del puntero sobre los colliders del mundo (<see cref="WorldPointerDispatcher"/>).
/// Equivalen 1:1 a los OnMouseXXX de Unity. Un componente implementa solo los que usa; el
/// dispatcher llama a todos los componentes del objeto que implementen la interfaz.
/// </summary>
public interface IWorldPointerDownHandler { void OnWorldPointerDown(); }

/// <summary>Cada frame con el botón apretado, siempre al objeto del Down.</summary>
public interface IWorldPointerDragHandler { void OnWorldPointerDrag(); }

/// <summary>Al soltar, al objeto del Down esté donde esté el puntero.</summary>
public interface IWorldPointerUpHandler { void OnWorldPointerUp(); }

/// <summary>Equivale a OnMouseUpAsButton: se soltó sobre el mismo objeto que se apretó.</summary>
public interface IWorldPointerClickHandler { void OnWorldPointerClick(); }

public interface IWorldPointerEnterHandler { void OnWorldPointerEnter(); }

/// <summary>Cada frame que el puntero sigue encima.</summary>
public interface IWorldPointerOverHandler { void OnWorldPointerOver(); }

public interface IWorldPointerExitHandler { void OnWorldPointerExit(); }

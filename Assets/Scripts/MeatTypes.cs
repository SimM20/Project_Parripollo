public enum MeatTypes
{
    Chorizo,
    Paty,
    Tira_De_Asado,
    Vacio,
    Chinchulin,
    Costilla_De_Cerdo,
    Pechuga_De_Pollo,
    Matambre
}

public enum MeatStates
{
    Crudo,      // 0 - no entregable
    Jugoso,     // 1 - solicitable
    Hecho,      // 2 - solicitable
    Muy_Hecho,  // 3 - solicitable ("Bien Hecho" en UI)
    Pasado,     // 4 - solicitable, último punto válido
    Quemado     // 5 - irreversible, no entregable
}
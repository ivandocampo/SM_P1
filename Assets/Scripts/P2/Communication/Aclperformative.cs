public enum ACLPerformative
{
    // === Informativos ===
    INFORM,             // Comunicar un hecho al receptor
    INFORM_DONE,        // Confirmar que una acción solicitada se completó
    INFORM_RESULT,      // Responder con el resultado de una consulta

    // === Solicitudes ===
    REQUEST,            // Pedir al receptor que ejecute una acción
    QUERY_IF,           // Preguntar si un predicado es verdadero
    QUERY_REF,          // Preguntar por el valor de una referencia

    // === Respuestas a solicitudes ===
    AGREE,              // Aceptar realizar la acción solicitada
    REFUSE,             // Rechazar la solicitud (con motivo)
    FAILURE,            // Informar de que la acción falló durante la ejecución

    // === Contract Net ===
    CFP,                // Call For Proposals: solicitar ofertas para una tarea
    PROPOSE,            // Enviar una propuesta en respuesta a un CFP
    ACCEPT_PROPOSAL,    // Aceptar la propuesta del receptor
    REJECT_PROPOSAL,    // Rechazar la propuesta del receptor

    // === Control ===
    CANCEL,             // Cancelar una solicitud previa
    NOT_UNDERSTOOD,     // Indicar que el mensaje no se comprendió

}
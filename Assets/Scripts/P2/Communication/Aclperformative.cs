// =============================================================
// Enumerado de performativas FIPA-ACL soportadas por el sistema.
// Define los actos comunicativos que puede transportar un ACLMessage:
// informacion, solicitudes, consultas, respuestas, Contract-Net y
// mensajes de control. ComunicacionAgente usa estos valores para
// despachar cada mensaje al manejador correspondiente
// =============================================================

public enum ACLPerformative
{
    INFORM,             // Comunicar un hecho al receptor
    INFORM_DONE,        // Confirmar que una acción solicitada se completó
    INFORM_RESULT,      // Responder con el resultado de una consulta

    REQUEST,            // Pedir al receptor que ejecute una acción
    QUERY_IF,           // Preguntar si un predicado es verdadero
    QUERY_REF,          // Preguntar por el valor de una referencia

    AGREE,              // Aceptar realizar la acción solicitada
    REFUSE,             // Rechazar la solicitud (con motivo)
    FAILURE,            // Informar de que la acción falló durante la ejecución

    CFP,                // Call For Proposals: solicitar ofertas para una tarea
    PROPOSE,            // Enviar una propuesta en respuesta a un CFP
    ACCEPT_PROPOSAL,    // Aceptar la propuesta del receptor
    REJECT_PROPOSAL,    // Rechazar la propuesta del receptor

    CANCEL,             // Cancelar una solicitud previa
    NOT_UNDERSTOOD,     // Indicar que el mensaje no se comprendió

}

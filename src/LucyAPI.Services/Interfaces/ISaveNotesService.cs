using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISaveNotesService
{
    SaveNotesResponse SaveAndEmail(string subject, string content);
}

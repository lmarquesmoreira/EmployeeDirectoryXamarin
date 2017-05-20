using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Xamarin.Forms;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Acr.UserDialogs;

using Microsoft.ProjectOxford.Face;

namespace EmployeeDirectory
{
    public class EmployeesViewModel : BaseViewModel
    {
        string personGroupId;

        public EmployeesViewModel()
        {
            Title = "Employees";

            Employees = new ObservableCollection<Employee>
            {
                new Employee { Name = "Felipe Grossi", Title = "SharePoint Analist", PhotoUrl = "https://scontent-gru2-1.xx.fbcdn.net/v/t1.0-9/11209391_691945937576100_2742638674919612467_n.jpg?oh=b4bbc7248615b31fb3e359aab8cf6500&oe=59B0BDE5" },
                new Employee { Name = "Lucas Marques", Title= "Software Analist", PhotoUrl = "https://scontent-gru2-1.xx.fbcdn.net/v/t1.0-9/1382927_590123731055018_1607069829_n.jpg?oh=b195dec2628c346936bdfc64f1da0e9f&oe=59BBDE9A"}
            };
        }

        ObservableCollection<Employee> employees;
        public ObservableCollection<Employee> Employees
        {
            get { return employees; }
            set { employees = value; OnPropertyChanged("Employees"); }
        }

        Command findSimilarFaceCommand;
        public Command FindSimilarFaceCommand
        {
            get
            {
                return findSimilarFaceCommand ?? (findSimilarFaceCommand = new Command(async () => await ExecuteFindSimilarFaceCommandAsync()));
            }
        }

        Command trainCommand;
        public Command TrainCommand
        {
            get
            {
                return trainCommand ?? (trainCommand = new Command(async () => await RegisterEmployees()));
            }
        }

        async Task ExecuteFindSimilarFaceCommandAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                MediaFile photo;

                await CrossMedia.Current.Initialize();

                // Take photo
                if (CrossMedia.Current.IsCameraAvailable)
                {
                    photo = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
                    {
                        Directory = "Employee Directory",
                        Name = "photo.jpg"
                    });
                }
                else
                {
                    photo = await CrossMedia.Current.PickPhotoAsync();
                }

                // Upload to cognitive services
                using (var stream = photo.GetStream())
                {
                    var faceServiceClient = new FaceServiceClient("74da8ff426ce4ca7a7a19d507102d029");

                    // Step 4 - Upload our photo and see who it is!
                    var faces = await faceServiceClient.DetectAsync(stream);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();

                    var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
                    var result = results[0].Candidates[0].PersonId;

                    var person = await faceServiceClient.GetPersonAsync(personGroupId, result);
                    UserDialogs.Instance.ShowSuccess($"Person identified is {person.Name}.");
                }
            }
            catch (Exception ex)
            {
                UserDialogs.Instance.ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        async Task RegisterEmployees()
        {
            try
            {
                UserDialogs.Instance.ShowLoading("Treinando...");
                var faceServiceClient = new FaceServiceClient("74da8ff426ce4ca7a7a19d507102d029");

                // Step 1 - Create Face List
                personGroupId = Guid.NewGuid().ToString();
                await faceServiceClient.CreatePersonGroupAsync(personGroupId, "Xamarin Employees");

                // Step 2 - Add people to face list
                foreach (var employee in Employees)
                {
                    var p = await faceServiceClient.CreatePersonAsync(personGroupId, employee.Name);
                    await faceServiceClient.AddPersonFaceAsync(personGroupId, p.PersonId, employee.PhotoUrl);
                }

                // Step 3 - Train face group
                await faceServiceClient.TrainPersonGroupAsync(personGroupId);

                UserDialogs.Instance.HideLoading();
                UserDialogs.Instance.ShowSuccess("Treinamento concluído com sucesso");

            }
            catch (Exception ex)
            {
                UserDialogs.Instance.HideLoading();
                UserDialogs.Instance.ShowError(ex.Message);
            }

        }
    }
}
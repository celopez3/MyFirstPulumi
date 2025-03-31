
# Creating Static Websites Using Pulumi Infrastructure as Code

This project was created for entry into the DEV Challenge sponsored by Pulumi. The main function of this project leverages the Pulumi .NET SDK to create index.html driven websites in the form of Google Cloud Bucket Storage allowing for rapid static website deployments from a single folder.




## Pre-Requisites & Requirements

This project is in C# and will require

    1. Visual Studio Code (although any Visual Studio will work)
    2. Pulumi CLI
    3. Google CLI
    4. Google Cloud <project-id>

Once you have installed the clients, you will need to make sure you have your Google Cloud Project ID. If you're new to Pulumi I would recommend reading my [article](https://dev.to/celopez3/using-pulumi-for-rapid-deployment) on Dev.to which covers the basic installation experience or you can get it straight from [Pulumi](https://www.pulumi.com/docs/iac/get-started/gcp/).

```bash
git clone https://github.com/celopez3/MyFirstPulumi.git
cd MyFirstPulumi
$env:GCP_PROJECT_ID="<project-id>"

```
    
## Deployment

To deploy this project run

```bash
pulumi config set gcp:project <project-id>
gcloud auth application-default login
pulumi up
```
If this is your first time running the Pulumi CLI it will ask you to use an existing stack from your Pulumi Cloud or to create one. Similarly if this is your first time using Pulumi with Google Cloud Platform after you set the configuration to your <project-id> you will need to setup the default login for your Google Cloud Platform.

## Usage/Examples

The primary purpose of the project is deploy all the files within the websites folder. You should be able to replace the contents of the websites folder with any number of static websites, just run `pulumi up` afterwards. Holding CTRL while hovering over links in the Powershell will the link in a browser.


using System.DirectoryServices;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string _adPath = "LDAP://vsmpo-avisma.ru";
            //string _adUser = "vs_ldp_esb_idm";
            string _adUser = "vs_ldp_esb_test";
            //string _adPwd = "BYk4#Gd4";
            string _adPwd = "NSRc!k6z";
            DirectoryEntry _de = new DirectoryEntry(_adPath, _adUser, _adPwd);
            DirectoryEntries _ds = _de.Children;
            DirectoryEntry _newUser;
            ;

            string _newUserName = userName.Text;

            string _createMessage = string.Format("CN=esb_{0},OU={1}", _newUserName, orgUnit.Text);
            try
            {
                _newUser = _ds.Find(_createMessage);
            }
            catch
            {
                _newUser = _ds.Add(_createMessage, "user");
            }
            _newUser.Properties["samAccountName"].Value = _newUserName;
            _newUser.Properties["kadr-id"].Value = 123456789;
            _newUser.Properties["department"].Value = "654";
            _newUser.CommitChanges();
            _de.CommitChanges();

        }
    }
}